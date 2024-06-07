using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataBaseLayer
{
    public class GenericDBList<TEntity> where TEntity : class
    {
        private string DbAddress { get; set; }
        private string ZipFilePath { get; set; }
        private string ZipEntryName => $"{typeof(TEntity).Name}.json";
        private bool UseZip { get; set; } // فلگ بولین برای انتخاب نوع ذخیره‌سازی
        /// <summary>
        /// Entity List from table
        /// </summary>
        public List<TEntity> Entities { get; set; } = new List<TEntity>();
        /// <summary>
        /// Last date and time when update table
        /// </summary>
        public DateTime? EntityLastUpdate { get; private set; }
        /// <summary>
        /// count all set and change items in table
        /// </summary>
        public int EntityTotalChange { get; private set; }

        PropertyInfo IDProperty => typeof(TEntity).GetProperties()
                             .FirstOrDefault(p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                                                  p.GetCustomAttributes(typeof(KeyAttribute), false).Any()) ?? throw new Exception("Cant Found ID or Key Attrib");
        /// <summary>
        /// default save in single file and true usezip 
        /// dont change this.
        /// </summary>
        /// <param name="useZip">Default true set total class in one file</param>
        /// <param name="dbAddress">default: Database/EntityName.json </param>
        /// <param name="zipFilePath">default: Database.simdb</param>
        public GenericDBList(bool useZip = true, string dbAddress = null, string zipFilePath = null)
        {
            UseZip = useZip; // مقداردهی فلگ
            DbAddress = dbAddress ?? $"Database/{typeof(TEntity).Name}.json";
            ZipFilePath = zipFilePath ?? "Database.simdb";
            Load();
        }

        /// <summary>
        /// Insert Data form Set and Save After set
        /// </summary>
        /// <param name="entity"></param>
        /// <returns> Null faild to insert </returns>
        public TEntity Insert(TEntity entity)
        {
            if (IDProperty != null && IDProperty.DeclaringType == typeof(EntityPublic))
            {
                (entity as EntityPublic)?.SetID(RecalculateNextID());
            }
            else if (IDProperty != null && IDProperty.CanWrite)
            {
                IDProperty.SetValue(entity, RecalculateNextID());
            }
            else
            {
                throw new Exception("Class Not PrimaryKey! set ID or set attrib [Key] top choice property");
            }

            Entities.Add(entity);
            Save();
            return entity;
        }
        /// <summary>
        /// Set Update and Save
        /// </summary>
        /// <param name="entity"></param>
        public void Update(TEntity entity)
        {
            var existingEntity = Entities.FirstOrDefault(e => (int)IDProperty.GetValue(e) == (int)IDProperty.GetValue(entity));
            if (existingEntity != null)
            {
                int index = Entities.IndexOf(existingEntity);
                Entities[index] = entity;
                Save();
            }
            else
                throw new Exception("Not Find EntityItem for change and update\r\n Please check data and try again");
        }
        /// <summary>
        /// Delete data and Save
        /// </summary>
        /// <param name="IDValue">primaryKey ID entity for delete</param>
        public void Delete(int IDValue)
        {
            var entity = Entities.FirstOrDefault(e => (int)IDProperty.GetValue(e) == IDValue);
            if (entity != null)
            {
                Entities.Remove(entity);
                Save();
            }
            else
                throw new Exception("Not Find Entity");
        }
        /// <summary>
        /// Update property item in Entity One Row and save items
        /// </summary>
        /// <param name="ID">Entity ID for edit property</param>
        /// <param name="Titem">if you wante change all property in entity</param>
        /// <param name="properties">property list for change and edit a=> a.sample = samle1 , a=>a.s2 = "s2" , ...</param>
        public void UpdateValueProperty(int ID, TEntity Titem, params Action<TEntity>[] properties)
        {
            var entity = GetByID(ID);

            if (entity == null) return;

            entity = Titem == null ? entity : Titem;

            foreach (var prp in properties)
            {
                prp(entity);
            }

            Update(entity);
        }
        /// <summary>
        /// Get all item and you can set query
        /// </summary>
        /// <param name="query">if u need set query you can start with</param>
        /// <returns></returns>
        public List<TEntity> GetAll(Func<TEntity, bool> query = null)
        {
            query ??= (a => true);
            return Entities.Where(query).ToList();
        }

        /// <summary>
        /// get just single item find by ID or Key
        /// </summary>
        /// <param name="IDValue"></param>
        /// <returns></returns>
        public TEntity GetByID(int IDValue)
        {
            return Entities.FirstOrDefault(e => (int)IDProperty.GetValue(e) == IDValue);
        }
        /// <summary>
        /// Find Max ID or Key for set next ID or key insert
        /// </summary>
        /// <returns>next key int</returns>
        /// <exception cref="InvalidOperationException">not find key</exception>
        public int RecalculateNextID()
        {
            if (IDProperty == null)
            {
                throw new InvalidOperationException("No ID property found.");
            }

            return Entities.Count > 0 ? Entities.Max(e => (int)IDProperty.GetValue(e)) + 1 : 1;
        }
        /// <summary>
        /// save data after change in entity
        /// 
        /// </summary>
        public void Save()
        {
            var data = new DatabaseFile<TEntity>
            {
                EntityData = Entities,
            };

            data.SetEntityTotalChange(EntityTotalChange);

            var option = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new NotMapPropertyConverter<TEntity>() }
            };

            string json = JsonSerializer.Serialize(data, option);

            if (UseZip)
            {
                // ذخیره‌سازی در فایل زیپ
                using (var zipToOpen = new FileStream(ZipFilePath, FileMode.OpenOrCreate))
                {
                    using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        var existingEntry = archive.GetEntry(ZipEntryName);
                        if (existingEntry != null)
                        {
                            existingEntry.Delete();
                        }

                        var entry = archive.CreateEntry(ZipEntryName);
                        using (var writer = new StreamWriter(entry.Open()))
                        {
                            writer.Write(json);
                        }
                    }
                }
            }
            else
            {
                // ذخیره‌سازی در فایل JSON مجزا
                File.WriteAllText(DbAddress, json);
            }
        }
        /// <summary>
        /// Load data from default address and get list entity
        /// </summary>
        /// <returns></returns>
        public List<TEntity> Load()
        {
            if (UseZip)
            {
                // بارگذاری از فایل زیپ
                using (var zipToOpen = new FileStream(ZipFilePath, FileMode.OpenOrCreate))
                {
                    using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        var entry = archive.GetEntry(ZipEntryName);
                        if (entry != null)
                        {
                            using (var reader = new StreamReader(entry.Open()))
                            {
                                string json = reader.ReadToEnd();
                                if (!string.IsNullOrEmpty(json))
                                {
                                    var data = JsonSerializer.Deserialize<DatabaseFile<TEntity>>(json);
                                    EntityLastUpdate = data?.EntityLastUpdate;
                                    EntityTotalChange = data?.EntityTotalChange ?? 0;
                                    if (data?.EntityData != null)
                                    {
                                        Entities = data.EntityData.DistinctBy(a => (int)IDProperty.GetValue(a)).ToList();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // بارگذاری از فایل JSON مجزا
                if (File.Exists(DbAddress))
                {
                    string json = File.ReadAllText(DbAddress);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var data = JsonSerializer.Deserialize<DatabaseFile<TEntity>>(json);
                        EntityLastUpdate = data?.EntityLastUpdate;
                        EntityTotalChange = data?.EntityTotalChange ?? 0;
                        if (data?.EntityData != null)
                        {
                            Entities = data.EntityData.DistinctBy(a => (int)IDProperty.GetValue(a)).ToList();
                        }
                    }
                }
                else
                {
                    if (!Directory.Exists("Database")) Directory.CreateDirectory("Database");
                    Entities = new List<TEntity>();
                    Save();
                }
            }
            return Entities;
        }
        /// <summary>
        /// Dispose and Save all change
        /// </summary>
        public void Dispose()
        {
            Save();
        }
    }
    /// <summary>
    /// defalut Temp for save and other detail
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class DatabaseFile<TEntity> where TEntity : class
    {
        public string EntityTypeName => typeof(TEntity).Name;
        [JsonInclude]
        public DateTime? EntityLastUpdate { get; private set; } = DateTime.Now;
        [JsonInclude]
        public int EntityTotalChange { get; private set; }
        public List<TEntity> EntityData { get; set; }

        internal void SetEntityTotalChange(int oldValue) => EntityTotalChange = oldValue + 1;
    }
    /// <summary>
    /// NotMap dont write and insert in table
    /// </summary>
    public class NotMapAttribute : Attribute { }
    internal class NotMapPropertyConverter<TEntity> : JsonConverter<TEntity> where TEntity : class
    {
        // این متد برای دی‌سریال‌سازی (بازگردانی) از JSON به شیء استفاده می‌شود.
        public override TEntity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // دی‌سریال‌سازی شیء از JSON
            return JsonSerializer.Deserialize<TEntity>(ref reader, options);
        }

        // این متد برای سریال‌سازی (تبدیل) شیء به JSON استفاده می‌شود.
        public override void Write(Utf8JsonWriter writer, TEntity value, JsonSerializerOptions options)
        {
            // دریافت تمام خصوصیات کلاس TEntity که دارای Attribute از نوع NotMap نیستند.
            var properties = typeof(TEntity).GetProperties()
                                            .Where(p => !Attribute.IsDefined(p, typeof(NotMapAttribute)) ||
                                                        !Attribute.IsDefined(p, typeof(NotMappedAttribute)));

            // شروع نوشتن یک شیء JSON
            writer.WriteStartObject();

            // برای هر خصوصیت، مقدار آن را به JSON اضافه می‌کنیم.
            foreach (var property in properties)
            {
                // گرفتن مقدار خصوصیت از شیء
                var propertyValue = property.GetValue(value);

                // نوشتن نام خصوصیت در JSON
                writer.WritePropertyName(property.Name);

                // سریال‌سازی مقدار خصوصیت و نوشتن آن در JSON
                JsonSerializer.Serialize(writer, propertyValue, property.PropertyType, options);
            }

            // پایان نوشتن شیء JSON
            writer.WriteEndObject();
        }
    }



}
