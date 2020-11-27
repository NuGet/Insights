using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class GenericSchemaSerializer
    {
        private readonly IReadOnlyDictionary<string, ISchema> NameToSchema;
        private readonly IReadOnlyDictionary<Type, ISchema> TypeToSchema;

        public GenericSchemaSerializer(IReadOnlyList<ISchema> schemas)
        {
            NameToSchema = schemas.ToDictionary(x => x.Name);
            TypeToSchema = schemas.ToDictionary(x => x.Type);
        }

        public ISerializedEntity Serialize<T>(T message)
        {
            if (!TypeToSchema.TryGetValue(typeof(T), out var schema))
            {
                throw new FormatException($"No schema for message type '{typeof(T).FullName}' exists.");
            }

            return schema.Serialize(message);
        }

        public object Deserialize(string message, ILogger logger)
        {
            var deserialized = JsonConvert.DeserializeObject<JObject>(message);

            var schemaName = deserialized.Value<string>("n");
            var schemaVersion = deserialized.Value<int>("v");
            var data = deserialized["d"];

            if (!NameToSchema.TryGetValue(schemaName, out var schema))
            {
                throw new FormatException($"The schema '{schemaName}' is not supported.");
            }

            var deserializedEntity = schema.Deserialize(schemaVersion, data);

            logger.LogInformation(
                "Deserialized object with schema {SchemaName} and version {SchemaVersion}.",
                schemaName,
                schemaVersion);

            return deserializedEntity;
        }
    }
}
