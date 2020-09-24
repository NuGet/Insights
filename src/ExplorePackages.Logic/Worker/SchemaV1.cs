using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class SchemaV1<T> : ISchema
    {
        private const int V1 = 1;
        private const int Latest = V1;

        private static readonly JsonSerializer JsonSerializer = new JsonSerializer
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            Converters =
            {
                new StringEnumConverter(),
            },
        };

        public SchemaV1(string name)
        {
            Name = name;
        }

        public Type Type { get; } = typeof(T);
        public string Name { get; }

        public ISerializedEntity Serialize(T message) => Serialize(Name, Latest, message);

        private static ISerializedEntity Serialize(string schemaName, int schemaVersion, T message)
        {
            return new SerializedMessage(() => JToken.FromObject(
                new DeserializedMessage(schemaName, schemaVersion, message),
                JsonSerializer));
        }

        public T Deserialize(int schemaVersion, JToken data)
        {
            if (schemaVersion != V1)
            {
                throw new FormatException($"The only version for schema '{Name}' supported is {V1}.");
            }

            return data.ToObject<T>(JsonSerializer);
        }

        ISerializedEntity ISchema.Serialize(object message) => Serialize((T)message);
        object ISchema.Deserialize(int schemaVersion, JToken json) => Deserialize(schemaVersion, json);

        private class DeserializedMessage
        {
            public DeserializedMessage(string schemaName, int schemaVersion, T data)
            {
                SchemaName = schemaName ?? throw new ArgumentNullException(nameof(schemaName));
                SchemaVersion = schemaVersion;
                Data = data;
            }

            [JsonProperty("n")]
            public string SchemaName { get; }

            [JsonProperty("v")]
            public int SchemaVersion { get; }

            [JsonProperty("d")]
            public T Data { get; }
        }
    }
}
