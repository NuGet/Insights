using System;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class SchemaV1<T> : ISchemaDeserializer, ISchemaSerializer<T>, ISchemaSerializer
    {
        private const int V1 = 1;

        public SchemaV1(string name)
        {
            Name = name;
        }

        public Type Type { get; } = typeof(T);
        public string Name { get; }
        public int LatestVersion { get; } = V1;

        public ISerializedEntity SerializeData(T message) => NameVersionSerializer.SerializeData(message);
        public ISerializedEntity SerializeMessage(T message) => NameVersionSerializer.SerializeMessage(Name, LatestVersion, message);

        public ISerializedEntity SerializeMessage(object message)
        {
            if (message.GetType() != Type)
            {
                throw new ArgumentException($"The provided message must be of type {Type.FullName}.");
            }

            return SerializeMessage((T)message);
        }

        public object Deserialize(int schemaVersion, JToken data)
        {
            if (schemaVersion != V1)
            {
                throw new FormatException($"The only version for schema '{Name}' supported is {V1}.");
            }

            return data.ToObject<T>(NameVersionSerializer.JsonSerializer);
        }
    }
}
