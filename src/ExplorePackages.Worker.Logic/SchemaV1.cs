using System;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class SchemaV1<T> : ISchema, ISchema<T>
    {
        private const int V1 = 1;

        public SchemaV1(string name)
        {
            Name = name;
        }

        public Type Type { get; } = typeof(T);
        public string Name { get; }
        public int LatestVersion { get; } = V1;

        public ISerializedEntity SerializeMessage(T message) => NameVersionSerializer.SerializeMessage(Name, LatestVersion, message);
        public JToken SerializeData(T message) => NameVersionSerializer.SerializeData(message);
        public T Deserialize(int schemaVersion, JToken data)
        {
            if (schemaVersion != V1)
            {
                throw new FormatException($"The only version for schema '{Name}' supported is {V1}.");
            }

            return data.ToObject<T>(NameVersionSerializer.JsonSerializer);
        }

        JToken ISchema.SerializeData(object message) => NameVersionSerializer.SerializeData((T)message);
        ISerializedEntity ISchema.SerializeMessage(object message) => NameVersionSerializer.SerializeMessage(Name, LatestVersion, (T)message);
        object ISchema.Deserialize(int schemaVersion, JToken json) => Deserialize(schemaVersion, json);

    }
}
