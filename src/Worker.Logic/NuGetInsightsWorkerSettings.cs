// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Insights.Worker
{
    public class NuGetInsightsWorkerSettings : NuGetInsightsSettings
    {
        public bool UseBulkEnqueueStrategy { get; set; } = true;
        public int BulkEnqueueThreshold { get; set; } = 10;
        public int EnqueueWorkers { get; set; } = 1;
        public int AppendResultStorageBucketCount { get; set; } = 1000; // Azure Data Explorer can only import up to 1000 blobs.
        public bool AllowBatching { get; set; } = true;
        public bool DisableMessageDelay { get; set; } = false;
        public bool RunAllCatalogScanDriversAsBatch { get; set; } = false;
        public bool OnlyKeepLatestInAuxiliaryFileUpdater { get; set; } = true;
        public bool RecordCertificateStatus { get; set; } = true;
        public bool MoveTempToHome { get; set; } = false;
        public List<CatalogScanDriverType> DisabledDrivers { get; set; } = new List<CatalogScanDriverType>();
        public int OldCatalogIndexScansToKeep { get; set; } = 49;
        public int OldWorkflowRunsToKeep { get; set; } = 49;
        public int WorkflowMaxAttempts { get; set; } = 5;

        public bool AutoStartCatalogScanUpdate { get; set; } = false;
        public bool AutoStartDownloadToCsv { get; set; } = false;
        public bool AutoStartOwnersToCsv { get; set; } = false;
        public bool AutoStartVerifiedPackagesToCsv { get; set; } = false;
        public TimeSpan CatalogScanUpdateFrequency { get; set; } = TimeSpan.FromHours(6);
        public TimeSpan DownloadToCsvFrequency { get; set; } = TimeSpan.FromHours(3);
        public TimeSpan OwnersToCsvFrequency { get; set; } = TimeSpan.FromHours(3);
        public TimeSpan VerifiedPackagesToCsvFrequency { get; set; } = TimeSpan.FromHours(3);
        public TimeSpan WorkflowFrequency { get; set; } = TimeSpan.FromDays(1);
        public TimeSpan KustoBlobIngestionTimeout { get; set; } = TimeSpan.FromHours(6);

        /// <summary>
        /// If the duration that the catalog scan covers (max cursor minus min cursor) is less than or equal to this
        /// threshold, telemetry events for each catalog leaf will be emitted at various stages of processing. Note that
        /// leaf level telemetry can be a lot of data, so this threshold should be relatively low, e.g. a bit longer
        /// than the normal catalog scan update cadence. The goal of this configuration value is to prevent leaf-level
        /// telemetry when you are reprocessing the entire catalog. Set this to "00:00:00" if you want to disable this
        /// kind of telemetry. If you are not sure, set it to twice the value of <see cref="CatalogScanUpdateFrequency"/>.
        /// </summary>
        public TimeSpan LeafLevelTelemetryThreshold { get; set; } = TimeSpan.FromHours(12);

        /// <summary>
        /// The types of package content to index in <see cref="PackageContentToCsv.PackageContentToCsvDriver"/>. The
        /// order of this list is significant (entries are processed in the matching order). The values are treated as
        /// file path suffixes so be sure to include a dot (".") if you want file extension behavior.
        /// </summary>
        public List<string> PackageContentFileExtensions { get; set; } = new List<string>();
        public int PackageContentMaxSizePerPackage { get; set; } = 1024 * 16;
        public int PackageContentMaxSizePerFile { get; set; } = 1024 * 16;

        public string WorkQueueName { get; set; } = "work";
        public string ExpandQueueName { get; set; } = "expand";
        public string CursorTableName { get; set; } = "cursors";
        public string CatalogIndexScanTableName { get; set; } = "catalogindexscans";
        public string CatalogPageScanTableName { get; set; } = "catalogpagescans";
        public string CatalogLeafScanTableName { get; set; } = "catalogleafscans";
        public string TaskStateTableName { get; set; } = "taskstate";
        public string CsvRecordTableName { get; set; } = "csvrecords";
        public string VersionSetAggregateTableName { get; set; } = "versionset";
        public string VersionSetContainerName { get; set; } = "versionset";
        public string KustoIngestionTableName { get; set; } = "kustoingestions";
        public string WorkflowRunTableName { get; set; } = "workflowruns";
        public string BucketedPackageTableName { get; set; } = "bucketedpackages";

        public string LatestPackageLeafTableName { get; set; } = "latestpackageleaves";
        public string PackageAssetContainerName { get; set; } = "packageassets";
        public string PackageAssemblyContainerName { get; set; } = "packageassemblies";
        public string PackageManifestContainerName { get; set; } = "packagemanifests";
        public string PackageReadmeContainerName { get; set; } = "packagereadmes";
        public string PackageLicenseContainerName { get; set; } = "packagelicenses";
        public string PackageSignatureContainerName { get; set; } = "packagesignatures";
        public string CatalogLeafItemContainerName { get; set; } = "catalogleafitems";
        public string PackageDownloadContainerName { get; set; } = "packagedownloads";
        public string PackageOwnerContainerName { get; set; } = "packageowners";
        public string VerifiedPackageContainerName { get; set; } = "verifiedpackages";
        public string PackageArchiveContainerName { get; set; } = "packagearchives";
        public string PackageArchiveEntryContainerName { get; set; } = "packagearchiveentries";
        public string SymbolPackageArchiveContainerName { get; set; } = "symbolpackagearchives";
        public string SymbolPackageArchiveEntryContainerName { get; set; } = "symbolpackagearchiveentries";
        public string PackageVersionTableName { get; set; } = "packageversions";
        public string PackageVersionContainerName { get; set; } = "packageversions";
        public string NuGetPackageExplorerContainerName { get; set; } = "nugetpackageexplorer";
        public string NuGetPackageExplorerFileContainerName { get; set; } = "nugetpackageexplorerfiles";
        public string PackageDeprecationContainerName { get; set; } = "packagedeprecations";
        public string PackageVulnerabilityContainerName { get; set; } = "packagevulnerabilities";
        public string PackageIconContainerName { get; set; } = "packageicons";
        public string PackageCompatibilityContainerName { get; set; } = "packagecompatibilities";
        public string PackageToCertificateTableName { get; set; } = "packagetocertificates";
        public string CertificateToPackageTableName { get; set; } = "certificatetopackages";
        public string PackageCertificateContainerName { get; set; } = "packagecertificates";
        public string CertificateContainerName { get; set; } = "certificates";
        public string PackageContentContainerName { get; set; } = "packagecontents";

        public string KustoConnectionString { get; set; } = null;
        public string KustoDatabaseName { get; set; } = null;
        public bool KustoUseUserManagedIdentity { get; set; } = true;

        /// <summary>
        /// A path to a certificate that will be loaded as a <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2"/>
        /// for Kusto AAD client app authentication.
        /// </summary>
        public string KustoClientCertificateContent { get; set; } = null;

        public bool KustoApplyPartitioningPolicy { get; set; } = true;
        public string KustoTableNameFormat { get; set; } = "{0}";
        public string KustoTableFolder { get; set; } = string.Empty;
        public string KustoTableDocstringFormat { get; set; } = "See https://github.com/NuGet/Insights/blob/main/docs/tables/{0}.md";
        public string KustoTempTableNameFormat { get; set; } = "{0}_Temp";
        public string KustoOldTableNameFormat { get; set; } = "{0}_Old";
        public int OldKustoIngestionsToKeep { get; set; } = 9;
        public int KustoIngestionMaxAttempts { get; set; } = 10;
        public int KustoValidationMaxAttempts { get; set; } = 3;

        public bool EnableDiagnosticTracingToLogger { get; set; } = false;
    }
}
