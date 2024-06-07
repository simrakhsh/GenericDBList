using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DataBaseLayer
{
    public interface IEntity
    {
        int ID { get; }
    }

    public class EntityPublic : IEntity
    {
        [JsonInclude]
        public int ID { get; private set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? RegDateTime { get; set; }
        public void SetID(int id) => ID = id;
    }
}
