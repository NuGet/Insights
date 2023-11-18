// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.CatalogDataToCsv;
using NuGet.Insights.Worker.EnqueueCatalogLeafScan;
using NuGet.Insights.Worker.ProcessBucketRange;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;
#if ENABLE_NPE
using NuGet.Insights.Worker.NuGetPackageExplorerToCsv;
#endif
using NuGet.Insights.Worker.PackageArchiveToCsv;
using NuGet.Insights.Worker.PackageAssemblyToCsv;
using NuGet.Insights.Worker.PackageAssetToCsv;
#if ENABLE_CRYPTOAPI
using NuGet.Insights.Worker.PackageCertificateToCsv;
#endif
using NuGet.Insights.Worker.PackageCompatibilityToCsv;
using NuGet.Insights.Worker.PackageContentToCsv;
using NuGet.Insights.Worker.PackageIconToCsv;
using NuGet.Insights.Worker.PackageLicenseToCsv;
using NuGet.Insights.Worker.PackageManifestToCsv;
using NuGet.Insights.Worker.PackageReadmeToCsv;
using NuGet.Insights.Worker.PackageSignatureToCsv;
using NuGet.Insights.Worker.PackageVersionToCsv;
using NuGet.Insights.Worker.ReferenceTracking;
using NuGet.Insights.Worker.SymbolPackageArchiveToCsv;
using NuGet.Insights.Worker.TableCopy;
using NuGet.Insights.Worker.TimedReprocess;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class SchemaCollectionBuilder
    {
        public static IReadOnlyList<ISchemaDeserializer> DefaultMessageSchemas { get; } = new ISchemaDeserializer[]
        {
            new SchemaV1<HomogeneousBulkEnqueueMessage>("hbe"),
            new SchemaV1<HeterogeneousBulkEnqueueMessage>("hebe"),
            new SchemaV1<HomogeneousBatchMessage>("hb"),

            new SchemaV1<CatalogIndexScanMessage>("cis"),
            new SchemaV1<CatalogPageScanMessage>("cps"),
            new SchemaV1<CatalogLeafScanMessage>("cls"),

            new SchemaV1<KustoIngestionMessage>("ki"),
            new SchemaV1<KustoContainerIngestionMessage>("kci"),
            new SchemaV1<KustoBlobIngestionMessage>("kbi"),

            new SchemaV1<TimedReprocessMessage>("tr"),

            new SchemaV1<WorkflowRunMessage>("wr"),

            new SchemaV1<CsvCompactMessage<CatalogLeafItemRecord>>("cc.fcli"),
            new SchemaV1<CsvCompactMessage<PackageArchiveRecord>>("cc.pa2c"),
            new SchemaV1<CsvCompactMessage<PackageArchiveEntry>>("cc.pae2c"),
            new SchemaV1<CsvCompactMessage<SymbolPackageArchiveRecord>>("cc.sa2c"),
            new SchemaV1<CsvCompactMessage<SymbolPackageArchiveEntry>>("cc.sae2c"),
            new SchemaV1<CsvCompactMessage<PackageAsset>>("cc.fpa"),
            new SchemaV1<CsvCompactMessage<PackageAssembly>>("cc.fpi"),
            new SchemaV1<CsvCompactMessage<PackageManifestRecord>>("cc.pm2c"),
            new SchemaV1<CsvCompactMessage<PackageReadme>>("cc.pmd2c"),
            new SchemaV1<CsvCompactMessage<PackageLicense>>("cc.pl2c"),
            new SchemaV1<CsvCompactMessage<PackageSignature>>("cc.fps"),
            new SchemaV1<CsvCompactMessage<PackageVersionRecord>>("cc.pv2c"),
            new SchemaV1<CsvCompactMessage<PackageDeprecationRecord>>("cc.pd2c"),
            new SchemaV1<CsvCompactMessage<PackageVulnerabilityRecord>>("cc.pu2c"),
            new SchemaV1<CsvCompactMessage<PackageIcon>>("cc.pi2c"),
            new SchemaV1<CsvCompactMessage<PackageCompatibility>>("cc.pc2c"),
            new SchemaV1<CsvCompactMessage<PackageContent>>("cc.pco2c"),
            
#if ENABLE_CRYPTOAPI
            new SchemaV1<CsvCompactMessage<CertificateRecord>>("cc.r2c"),
            new SchemaV1<CsvCompactMessage<PackageCertificateRecord>>("cc.pr2c"),

            new SchemaV1<CleanupOrphanRecordsMessage<CertificateRecord>>("co.r"),
#endif

#if ENABLE_NPE
            new SchemaV1<CsvCompactMessage<NuGetPackageExplorerRecord>>("cc.npe2c"),
            new SchemaV1<CsvCompactMessage<NuGetPackageExplorerFile>>("cc.npef2c"),
#endif

            new SchemaV1<TableScanMessage<CatalogLeafScan>>("ts.cls"),
            new SchemaV1<TableScanMessage<BucketedPackage>>("ts.bp"),
            new SchemaV1<TableScanMessage<LatestPackageLeaf>>("ts.lpf"),

            new SchemaV1<TableRowCopyMessage<LatestPackageLeaf>>("trc.lpf"),

            new SchemaV1<AuxiliaryFileUpdaterMessage<AsOfData<PackageDownloads>>>("d2c"),
            new SchemaV1<AuxiliaryFileUpdaterMessage<AsOfData<PackageOwner>>>("o2c"),
            new SchemaV1<AuxiliaryFileUpdaterMessage<AsOfData<VerifiedPackage>>>("vp2c"),
        };

        public static IReadOnlyList<ISchemaDeserializer> DefaultParameterSchemas { get; } = new ISchemaDeserializer[]
        {
            new SchemaV1<TablePrefixScanStartParameters>("tps.s"),
            new SchemaV1<TablePrefixScanPartitionKeyQueryParameters>("tps.pkq"),
            new SchemaV1<TablePrefixScanPrefixQueryParameters>("tps.pq"),

            new SchemaV1<EnqueueCatalogLeafScansParameters>("ecls"),
            new SchemaV1<ProcessBucketRangeParameters>("etrb"),
            new SchemaV1<TableCopyParameters>("tc"),

            new SchemaV1<CleanupOrphanRecordsParameters>("cor"),
        };

        public static SchemaCollectionBuilder Default => new SchemaCollectionBuilder()
            .AddRange(DefaultMessageSchemas)
            .AddRange(DefaultParameterSchemas);

        private readonly List<ISchemaDeserializer> _schemas = new();

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

        public SchemaCollection Build()
        {
            return new SchemaCollection(_schemas);
        }
    }
}
