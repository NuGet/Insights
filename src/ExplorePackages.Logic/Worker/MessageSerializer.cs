using AngleSharp.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class MessageSerializer
    {
        private interface ISchema
        {
            string Name { get; }
            Type Type { get; }
            byte[] Serialize(object message);
            object Deserialize(int schemaVersion, JToken json);
        }

        private class SchemaV1<T> : ISchema
        {
            public SchemaV1()
            {
                Name = GetName();
            }

            private const int V1 = 1;
            private const int Latest = V1;

            public Type Type { get; } = typeof(T);
            public string Name { get; }

            public byte[] Serialize(T message)
            {
                return GetBytes(Name, Latest, message);
            }

            public T Deserialize(int schemaVersion, JToken json)
            {
                if (schemaVersion != V1)
                {
                    throw new FormatException($"The only version for schema '{Name}' supported is {V1}.");
                }

                return json.ToObject<T>();
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

            byte[] ISchema.Serialize(object message) => Serialize((T)message);
            object ISchema.Deserialize(int schemaVersion, JToken json) => Deserialize(schemaVersion, json);
        }

        private static readonly IReadOnlyList<ISchema> Schemas = new ISchema[]
        {
            new SchemaV1<CatalogIndexScanMessage>(),
            new SchemaV1<CatalogPageScanMessage>(),
        };

        private static readonly IReadOnlyDictionary<string, ISchema> NameToSchema = Schemas.ToDictionary(x => x.Name);
        private static readonly IReadOnlyDictionary<Type, ISchema> TypeToSchema = Schemas.ToDictionary(x => x.Type);

        public byte[] Serialize(CatalogIndexScanMessage message) => SerializeInternal(message);
        public byte[] Serialize(CatalogPageScanMessage message) => SerializeInternal(message);

        private byte[] SerializeInternal<T>(T message)
        {
            if (!TypeToSchema.TryGetValue(typeof(T), out var schema))
            {
                throw new FormatException($"No schema for message type '{typeof(T).FullName}' exists.");
            }

            return schema.Serialize(message);
        }

        public object Deserialize(byte[] message)
        {
            var json = Encoding.UTF8.GetString(message);
            var deserialized = JsonConvert.DeserializeObject<JObject>(json);

            var schemaName = deserialized.Value<string>("SchemaName");
            var schemaVersion = deserialized.Value<int>("SchemaVersion");
            var value = deserialized["Value"];

            if (!NameToSchema.TryGetValue(schemaName, out var schema))
            {
                throw new FormatException($"The schema '{schemaName}' is not supported.");
            }

            return schema.Deserialize(schemaVersion, value);
        }

        private static byte[] GetBytes<T>(string schemaName, int schemaVersion, T message)
        {
            var json = JsonConvert.SerializeObject(new DeserializedMessage<T>(
                schemaName,
                schemaVersion,
                message));

            return Encoding.UTF8.GetBytes(json);
        }

        private class DeserializedMessage<T>
        {
            public DeserializedMessage(string schemaName, int schemaVersion, T value)
            {
                SchemaName = schemaName ?? throw new ArgumentNullException(nameof(schemaName));
                SchemaVersion = schemaVersion;
                Value = value;
            }

            public string SchemaName { get; }
            public int SchemaVersion { get; }
            public T Value { get; }
        }
    }
}
