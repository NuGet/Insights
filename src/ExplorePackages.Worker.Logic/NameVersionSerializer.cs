using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public static class NameVersionSerializer
    {
        public static readonly JsonSerializer JsonSerializer = new JsonSerializer
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            Converters =
            {
                new StringEnumConverter(),
            },
        };

        public static JToken SerializeData<T>(T message) => JToken.FromObject(message, JsonSerializer);
        public static ISerializedEntity SerializeMessage<T>(string name, int version, T message)
        {
            return new SerializedMessage(() => JToken.FromObject(
                new DeserializedMessage<T>(name, version, message),
                JsonSerializer));
        }

        private class DeserializedMessage<T>
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
