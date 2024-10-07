// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.CopyBucketRange;
using NuGet.Insights.Worker.EnqueueCatalogLeafScan;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;
using NuGet.Insights.Worker.ReferenceTracking;
using NuGet.Insights.Worker.TableCopy;
using NuGet.Insights.Worker.TimedReprocess;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class SchemaCollectionBuilder
    {
        public static IReadOnlyList<ISchemaDeserializer> DefaultMessageSchemas { get; } =
        [
            new SchemaV1<HomogeneousBulkEnqueueMessage>("hbe"),
            new SchemaV1<HeterogeneousBulkEnqueueMessage>("htbe"),
            new SchemaV1<HomogeneousBatchMessage>("hb"),

            new SchemaV1<CatalogIndexScanMessage>("cis"),
            new SchemaV1<CatalogPageScanMessage>("cps"),
            new SchemaV1<CatalogLeafScanMessage>("cls"),

            new SchemaV1<KustoIngestionMessage>("ki"),
            new SchemaV1<KustoContainerIngestionMessage>("kci"),
            new SchemaV1<KustoBlobIngestionMessage>("kbi"),

            new SchemaV1<TimedReprocessMessage>("tr"),

            new SchemaV1<WorkflowRunMessage>("wr"),

            new SchemaV1<TableScanMessage<CatalogLeafScan>>("ts.cls"),
            new SchemaV1<TableScanMessage<BucketedPackage>>("ts.bp"),
            new SchemaV1<TableScanMessage<LatestPackageLeaf>>("ts.lpf"),

            new SchemaV1<TableRowCopyMessage<LatestPackageLeaf>>("trc.lpf"),

            new SchemaV1<AuxiliaryFileUpdaterMessage<AsOfData<PackageDownloads>>>("d2c"),
            new SchemaV1<AuxiliaryFileUpdaterMessage<AsOfData<PackageOwner>>>("o2c"),
            new SchemaV1<AuxiliaryFileUpdaterMessage<AsOfData<VerifiedPackage>>>("vp2c"),
            new SchemaV1<AuxiliaryFileUpdaterMessage<AsOfData<ExcludedPackage>>>("ep2c"),
            new SchemaV1<AuxiliaryFileUpdaterMessage<AsOfData<PopularityTransfer>>>("pt2c"),

            .. GetCsvCompactMessageSchemaForDrivers(),
            .. GetCleanupOrphanRecordsMessageSchemaForDrivers(),
        ];

        public static IReadOnlyList<ISchemaDeserializer> DefaultParameterSchemas { get; } =
        [
            new SchemaV1<TablePrefixScanStartParameters>("tps.s"),
            new SchemaV1<TablePrefixScanPartitionKeyQueryParameters>("tps.pkq"),
            new SchemaV1<TablePrefixScanPrefixQueryParameters>("tps.pq"),

            new SchemaV1<EnqueueCatalogLeafScansParameters>("ecls"),
            new SchemaV1<CopyBucketRangeParameters>("etrb"),
            new SchemaV1<TableCopyParameters>("tc"),

            new SchemaV1<CleanupOrphanRecordsParameters>("cor"),
        ];

        public static SchemaCollectionBuilder Default => new SchemaCollectionBuilder()
            .AddRange(DefaultMessageSchemas)
            .AddRange(DefaultParameterSchemas);

        private readonly List<ISchemaDeserializer> _schemas = new();

        private static IEnumerable<ISchemaDeserializer> GetCsvCompactMessageSchemaForDrivers()
        {
            var method = typeof(SchemaCollectionBuilder).GetMethod(nameof(GetCsvCompactMessageSchemaName), BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var recordType in GetCsvRecordTypes())
            {
                if (recordType.IsAssignableTo(typeof(IAggregatedCsvRecord<>).MakeGenericType(recordType)))
                {
                    var schemaName = (string)method.MakeGenericMethod(recordType).Invoke(null, null);
                    var messageType = typeof(CsvCompactMessage<>).MakeGenericType(recordType);
                    var schemaType = typeof(SchemaV1<>).MakeGenericType(messageType);
                    yield return (ISchemaDeserializer)Activator.CreateInstance(schemaType, schemaName);
                }
            }
        }

        private static string GetCsvCompactMessageSchemaName<T>() where T : IAggregatedCsvRecord<T>
        {
            return T.CsvCompactMessageSchemaName;
        }

        private static IEnumerable<ISchemaDeserializer> GetCleanupOrphanRecordsMessageSchemaForDrivers()
        {
            var method = typeof(SchemaCollectionBuilder).GetMethod(nameof(GetCleanupOrphanRecordsMessageSchemaName), BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var recordType in GetCsvRecordTypes())
            {
                if (recordType.IsAssignableTo(typeof(ICleanupOrphanCsvRecord)))
                {
                    var schemaName = (string)method.MakeGenericMethod(recordType).Invoke(null, null);
                    var messageType = typeof(CleanupOrphanRecordsMessage<>).MakeGenericType(recordType);
                    var schemaType = typeof(SchemaV1<>).MakeGenericType(messageType);
                    yield return (ISchemaDeserializer)Activator.CreateInstance(schemaType, schemaName);
                }
            }
        }

        private static string GetCleanupOrphanRecordsMessageSchemaName<T>() where T : ICleanupOrphanCsvRecord
        {
            return T.CleanupOrphanRecordsMessageSchemaName;
        }

        private static IEnumerable<Type> GetCsvRecordTypes()
        {
            foreach (var type in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                var recordTypes = CatalogScanDriverMetadata.GetRecordTypes(type);
                if (recordTypes is null)
                {
                    continue;
                }

                foreach (var recordType in recordTypes)
                {
                    yield return recordType;
                }
            }
        }

        public SchemaCollectionBuilder Add<T>(SchemaV1<T> schema)
        {
            _schemas.Add(schema);
            return this;
        }

        public SchemaCollectionBuilder AddRange(IEnumerable<ISchemaDeserializer> schemas)
        {
            _schemas.AddRange(schemas);
            return this;
        }

        public IEnumerable<ISchemaDeserializer> GetDeserializers()
        {
            return _schemas;
        }

        public SchemaCollection Build()
        {
            return new SchemaCollection(_schemas);
        }
    }
}
