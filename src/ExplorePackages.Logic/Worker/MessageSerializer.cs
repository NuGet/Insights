using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class MessageSerializer
    {
        private static class PackageQuerySchema
        {
            public const string Name = "PackageQuery";
            private const int V1 = 1;
            private const int Latest = V1;

            public static byte[] Serialize(PackageQueryMessage message)
            {
                return GetBytes(Name, Latest, message);
            }

            public static PackageQueryMessage Deserialize(int schemaVersion, JToken json)
            {
                if (schemaVersion != V1)
                {
                    throw new FormatException($"The only version for schema '{Name}' supported is {V1}.");
                }

                return json.ToObject<PackageQueryMessage>();
            }
        }

        public byte[] Serialize(PackageQueryMessage message) => PackageQuerySchema.Serialize(message);

        public object Deserialize(byte[] message)
        {
            var json = Encoding.UTF8.GetString(message);
            var deserialized = JsonConvert.DeserializeObject<JObject>(json);

            var schemaName = deserialized.Value<string>("SchemaName");
            var schemaVersion = deserialized.Value<int>("SchemaVersion");
            var value = deserialized["Value"];

            switch (schemaName)
            {
                case PackageQuerySchema.Name:
                    return PackageQuerySchema.Deserialize(schemaVersion, value);
                default:
                    throw new FormatException($"The schema '{schemaName}' is not supported.");
            }
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
