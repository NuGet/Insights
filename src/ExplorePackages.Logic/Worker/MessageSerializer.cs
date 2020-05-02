using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class MessageSerializer
    {
        private static readonly IReadOnlyList<ISchema> Schemas = new ISchema[]
        {
            new SchemaV1<BulkEnqueueMessage>(),
            new SchemaV1<CatalogIndexScanMessage>(),
            new SchemaV1<CatalogPageScanMessage>(),
            new SchemaV1<CatalogLeafMessage>(),
        };

        private static readonly IReadOnlyDictionary<string, ISchema> NameToSchema = Schemas.ToDictionary(x => x.Name);
        private static readonly IReadOnlyDictionary<Type, ISchema> TypeToSchema = Schemas.ToDictionary(x => x.Type);
        private readonly ILogger<MessageSerializer> _logger;

        public MessageSerializer(ILogger<MessageSerializer> logger)
        {
            _logger = logger;
        }

        public ISerializedMessage Serialize(BulkEnqueueMessage message) => SerializeInternal(message);
        public ISerializedMessage Serialize(CatalogIndexScanMessage message) => SerializeInternal(message);
        public ISerializedMessage Serialize(CatalogPageScanMessage message) => SerializeInternal(message);
        public ISerializedMessage Serialize(CatalogLeafMessage message) => SerializeInternal(message);

        private ISerializedMessage SerializeInternal<T>(T message)
        {
            if (!TypeToSchema.TryGetValue(typeof(T), out var schema))
            {
                throw new FormatException($"No schema for message type '{typeof(T).FullName}' exists.");
            }

            return schema.Serialize(message);
        }

        public object Deserialize(string message)
        {
            var deserialized = JsonConvert.DeserializeObject<JObject>(message);

            var schemaName = deserialized.Value<string>("n");
            var schemaVersion = deserialized.Value<int>("v");
            var data = deserialized["d"];

            if (!NameToSchema.TryGetValue(schemaName, out var schema))
            {
                throw new FormatException($"The schema '{schemaName}' is not supported.");
            }

            var deserializedMessage = schema.Deserialize(schemaVersion, data);

            _logger.LogInformation(
                "Deserialized message with schema {SchemaName} and version {SchemaVersion}.",
                schemaName,
                schemaVersion);

            return deserializedMessage;
        }

        private interface ISchema
        {
            string Name { get; }
            Type Type { get; }
            ISerializedMessage Serialize(object message);
            object Deserialize(int schemaVersion, JToken data);
        }

        private class SchemaV1<T> : ISchema
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

            public SchemaV1()
            {
                Name = GetName();
            }

            public Type Type { get; } = typeof(T);
            public string Name { get; }

            public ISerializedMessage Serialize(T message) => Serialize(Name, Latest, message);

            private static ISerializedMessage Serialize(string schemaName, int schemaVersion, T message)
            {
                return new SerializedMessage(() => JToken.FromObject(
                    new DeserializedMessage<T>(schemaName, schemaVersion, message),
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

            private static string GetName()
            {
                const string suffix = "Message";
                var typeName = typeof(T).Name;
                if (!typeName.EndsWith(suffix))
                {
                    throw new ArgumentException($"The message type name must end with '{suffix}'.");
                }

                return typeName.Substring(0, typeName.Length - suffix.Length);
            }

            ISerializedMessage ISchema.Serialize(object message) => Serialize((T)message);
            object ISchema.Deserialize(int schemaVersion, JToken json) => Deserialize(schemaVersion, json);
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
