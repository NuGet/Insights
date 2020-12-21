using System;
using System.Collections.Generic;
using System.Linq;
using Knapcode.ExplorePackages.Worker.FindPackageAssets;
using Knapcode.ExplorePackages.Worker.RunRealRestore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class SchemaSerializer
    {
        private static readonly SchemasCollection Schemas = new SchemasCollection(new ISchemaDeserializer[]
        {
            new SchemaV1<MixedBulkEnqueueMessage>("mbe"),
            new SchemaV1<HomogeneousBulkEnqueueMessage>("hbe"),
            new SchemaV1<HomogeneousBatchMessage>("hb"),

            new SchemaV1<CatalogIndexScanMessage>("cis"),
            new SchemaV1<CatalogPageScanMessage>("cps"),
            new SchemaV1<CatalogLeafScanMessage>("cls"),

            new SchemaV1<FindPackageAssetsCompactMessage>("fpa.c"),

            new SchemaV1<RunRealRestoreMessage>("rrr"),
            new SchemaV1<RunRealRestoreCompactMessage>("rrr.c"),

            new SchemaV1<FindLatestLeavesParameters>("fll"),
            new SchemaV1<FindPackageAssetsParameters>("fpa"),
        });

        private readonly ILogger<SchemaSerializer> _logger;

        public SchemaSerializer(ILogger<SchemaSerializer> logger)
        {
            _logger = logger;
        }

        public ISchemaSerializer<T> GetSerializer<T>() => Schemas.GetSerializer<T>();
        public ISerializedEntity Serialize<T>(T message) => Schemas.GetSerializer<T>().SerializeMessage(message);
        public object Deserialize(string message) => Schemas.Deserialize(message, _logger);
        public object Deserialize(NameVersionMessage<JToken> message) => Schemas.Deserialize(message, _logger);

        private class SchemasCollection
        {
            private readonly IReadOnlyDictionary<string, ISchemaDeserializer> NameToSchema;
            private readonly IReadOnlyDictionary<Type, ISchemaDeserializer> TypeToSchema;

            public SchemasCollection(IReadOnlyList<ISchemaDeserializer> schemas)
            {
                NameToSchema = schemas.ToDictionary(x => x.Name);
                TypeToSchema = schemas.ToDictionary(x => x.Type);
            }

            public ISchemaSerializer<T> GetSerializer<T>()
            {
                if (!TypeToSchema.TryGetValue(typeof(T), out var genericSchema))
                {
                    throw new FormatException($"No schema for message type '{typeof(T).FullName}' exists.");
                }

                var typedScheme = genericSchema as ISchemaSerializer<T>;
                if (typedScheme == null)
                {
                    throw new FormatException($"The schema for message type '{typeof(T).FullName}' is not a typed schema.");
                }

                return typedScheme;
            }

            public object Deserialize(string message, ILogger logger)
            {
                return Deserialize(NameVersionSerializer.DeserializeMessage(message), logger);
            }

            public object Deserialize(NameVersionMessage<JToken> message, ILogger logger)
            {
                if (!NameToSchema.TryGetValue(message.SchemaName, out var schema))
                {
                    throw new FormatException($"The schema '{message.SchemaName}' is not supported.");
                }

                var deserializedEntity = schema.Deserialize(message.SchemaVersion, message.Data);

                logger.LogInformation(
                    "Deserialized object with schema {SchemaName} and version {SchemaVersion}.",
                    message.SchemaName,
                    message.SchemaVersion);

                return deserializedEntity;
            }
        }
    }
}
