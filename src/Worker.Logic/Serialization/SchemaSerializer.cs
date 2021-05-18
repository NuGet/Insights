// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.CatalogLeafItemToCsv;
using NuGet.Insights.Worker.EnqueueCatalogLeafScan;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;
using NuGet.Insights.Worker.NuGetPackageExplorerToCsv;
using NuGet.Insights.Worker.PackageArchiveToCsv;
using NuGet.Insights.Worker.PackageAssemblyToCsv;
using NuGet.Insights.Worker.PackageAssetToCsv;
using NuGet.Insights.Worker.PackageManifestToCsv;
using NuGet.Insights.Worker.PackageSignatureToCsv;
using NuGet.Insights.Worker.PackageVersionToCsv;
using NuGet.Insights.Worker.TableCopy;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class SchemaSerializer
    {
        public static IReadOnlyList<ISchemaDeserializer> MessageSchemas = new ISchemaDeserializer[]
        {
            new SchemaV1<HomogeneousBulkEnqueueMessage>("hbe"),
            new SchemaV1<HomogeneousBatchMessage>("hb"),

            new SchemaV1<CatalogIndexScanMessage>("cis"),
            new SchemaV1<CatalogPageScanMessage>("cps"),
            new SchemaV1<CatalogLeafScanMessage>("cls"),

            new SchemaV1<KustoIngestionMessage>("ki"),
            new SchemaV1<KustoContainerIngestionMessage>("kci"),
            new SchemaV1<KustoBlobIngestionMessage>("kbi"),

            new SchemaV1<WorkflowRunMessage>("wr"),

            new SchemaV1<CsvCompactMessage<CatalogLeafItemRecord>>("cc.fcli"),
            new SchemaV1<CsvCompactMessage<PackageArchiveRecord>>("cc.pa2c"),
            new SchemaV1<CsvCompactMessage<PackageArchiveEntry>>("cc.pae2c"),
            new SchemaV1<CsvCompactMessage<PackageAsset>>("cc.fpa"),
            new SchemaV1<CsvCompactMessage<PackageAssembly>>("cc.fpi"),
            new SchemaV1<CsvCompactMessage<PackageManifestRecord>>("cc.pm2c"),
            new SchemaV1<CsvCompactMessage<PackageSignature>>("cc.fps"),
            new SchemaV1<CsvCompactMessage<PackageVersionRecord>>("cc.pv2c"),
            new SchemaV1<CsvCompactMessage<NuGetPackageExplorerRecord>>("cc.npe2c"),
            new SchemaV1<CsvCompactMessage<NuGetPackageExplorerFile>>("cc.npef2c"),

            new SchemaV1<CsvExpandReprocessMessage<NuGetPackageExplorerRecord>>("cer.npe2c"),
            new SchemaV1<CsvExpandReprocessMessage<NuGetPackageExplorerFile>>("cer.npef2c"),

            new SchemaV1<TableScanMessage<CatalogLeafScan>>("ts.cls"),
            new SchemaV1<TableScanMessage<LatestPackageLeaf>>("ts.lpf"),

            new SchemaV1<TableRowCopyMessage<LatestPackageLeaf>>("trc.lpf"),

            new SchemaV1<AuxiliaryFileUpdaterMessage<PackageDownloadSet>>("d2c"),
            new SchemaV1<AuxiliaryFileUpdaterMessage<PackageOwnerSet>>("o2c"),
        };

        public static IReadOnlyList<ISchemaDeserializer> ParameterSchemas = new ISchemaDeserializer[]
        {
            new SchemaV1<TablePrefixScanStartParameters>("tps.s"),
            new SchemaV1<TablePrefixScanPartitionKeyQueryParameters>("tps.pkq"),
            new SchemaV1<TablePrefixScanPrefixQueryParameters>("tps.pq"),

            new SchemaV1<CatalogLeafToCsvParameters>("cl2c"),

            new SchemaV1<EnqueueCatalogLeafScansParameters>("ecls"),
            new SchemaV1<TableCopyParameters>("tc"),
        };

        private static readonly SchemasCollection Schemas = new SchemasCollection(Enumerable
            .Empty<ISchemaDeserializer>()
            .Concat(MessageSchemas)
            .Concat(ParameterSchemas)
            .ToList());

        private readonly ILogger<SchemaSerializer> _logger;

        public SchemaSerializer(ILogger<SchemaSerializer> logger)
        {
            _logger = logger;
        }

        public ISchemaSerializer<T> GetSerializer<T>()
        {
            return Schemas.GetSerializer<T>();
        }

        public ISchemaSerializer GetGenericSerializer(Type type)
        {
            return Schemas.GetSerializer(type);
        }

        public ISerializedEntity Serialize<T>(T message)
        {
            return Schemas.GetSerializer<T>().SerializeMessage(message);
        }

        public ISchemaDeserializer GetDeserializer(string schemaName)
        {
            return Schemas.GetDeserializer(schemaName);
        }

        public NameVersionMessage<object> Deserialize(string message)
        {
            return Schemas.Deserialize(message, _logger);
        }

        public NameVersionMessage<object> Deserialize(JToken message)
        {
            return Schemas.Deserialize(message, _logger);
        }

        public NameVersionMessage<object> Deserialize(NameVersionMessage<JToken> message)
        {
            return Schemas.Deserialize(message, _logger);
        }

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
                if (!TypeToSchema.TryGetValue(typeof(T), out var schema))
                {
                    throw new FormatException($"No schema for message type '{typeof(T).FullName}' exists.");
                }

                var typedSchema = schema as ISchemaSerializer<T>;
                if (typedSchema == null)
                {
                    throw new FormatException($"The schema for message type '{typeof(T).FullName}' is not a typed schema.");
                }

                return typedSchema;
            }

            public ISchemaSerializer GetSerializer(Type type)
            {
                if (!TypeToSchema.TryGetValue(type, out var schema))
                {
                    throw new FormatException($"No schema for message type '{type.FullName}' exists.");
                }

                var genericSchema = schema as ISchemaSerializer;
                if (genericSchema == null)
                {
                    throw new FormatException($"The schema for message type '{type.FullName}' is not a generic schema.");
                }

                return genericSchema;
            }

            public ISchemaDeserializer GetDeserializer(string schemaName)
            {
                if (!NameToSchema.TryGetValue(schemaName, out var schema))
                {
                    throw new FormatException($"The schema '{schemaName}' is not supported.");
                }

                return schema;
            }

            public NameVersionMessage<object> Deserialize(string message, ILogger logger)
            {
                return Deserialize(NameVersionSerializer.DeserializeMessage(message), logger);
            }

            public NameVersionMessage<object> Deserialize(JToken message, ILogger logger)
            {
                return Deserialize(NameVersionSerializer.DeserializeMessage(message), logger);
            }

            public NameVersionMessage<object> Deserialize(NameVersionMessage<JToken> message, ILogger logger)
            {
                var schema = GetDeserializer(message.SchemaName);
                var deserializedEntity = schema.Deserialize(message.SchemaVersion, message.Data);

                logger.LogInformation(
                    "Deserialized object with schema {SchemaName} and version {SchemaVersion}.",
                    message.SchemaName,
                    message.SchemaVersion);

                return new NameVersionMessage<object>(message.SchemaName, message.SchemaVersion, deserializedEntity);
            }
        }
    }
}
