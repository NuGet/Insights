// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings : NuGetInsightsSettings
    {
        public bool UseBulkEnqueueStrategy { get; set; } = true;
        public int BulkEnqueueThreshold { get; set; } = 10;
        public int EnqueueWorkers { get; set; } = 1;
        public int MaxBulkEnqueueMessageCount { get; set; } = 50;
        public int AppendResultStorageBucketCount { get; set; } = 1000; // Azure Data Explorer can only import up to 1000 blobs.
        public int AppendResultBigModeRecordThreshold { get; set; } = 25_000;
        public int AppendResultBigModeSubdivisionSize { get; set; } = 10_000;
        public int TableScanTakeCount { get; set; } = StorageUtility.MaxTakeCount;
        public bool AllowBatching { get; set; } = true;
        public bool DisableMessageDelay { get; set; } = false;
        public bool OnlyKeepLatestInAuxiliaryFileUpdater { get; set; } = true;
        public bool RecordCertificateStatus { get; set; } = true;
        public bool MoveTempToHome { get; set; } = false;
        public List<CatalogScanDriverType> DisabledDrivers { get; set; } = new List<CatalogScanDriverType>();
        public int OldCatalogIndexScansToKeep { get; set; } = 49;
        public int OldWorkflowRunsToKeep { get; set; } = 49;
        public int WorkflowMaxAttempts { get; set; } = 5;

        public bool TimedReprocessIsEnabled { get; set; } = true;

        /// <summary>
        /// Don't set the Content-MD5 header in output CSVs (e.g. for <see cref="PackageArchiveToCsv"/>). The header
        /// appears to be returned inconsistently from some CDN endpoints leading to test flakiness.
        /// </summary>
        public bool SkipContentMD5HeaderInCsv { get; set; } = false;

        /// <summary>
        /// This is the desired amount of time it will take to reprocess all packages. For content like legacy README or
        /// symbol packages that can be modified without any event in the catalog, this is the maximum staleness of that
        /// information stored in NuGet.Insights.
        /// </summary>
        public TimeSpan TimedReprocessWindow { get; set; } = TimeSpan.FromDays(14);

        /// <summary>
        /// This is the frequency that the timed preprocess service processes a set of package buckets. If you're using
        /// the workflow system, this configuration value is overridden by <see cref="WorkflowFrequency"/>.
        /// </summary>
        public TimeSpan TimedReprocessFrequency { get; set; } = TimeSpan.FromHours(6);

        /// <summary>
        /// This is the maximum number of buckets to reprocess in a single execution. This configuration exists so that if
        /// the reprocessing flow is for a long time or takes too long, the next attempt won't overload the system by
        /// reprocessing too many packages at once. This number should be larger than <see cref="TimedReprocessWindow"/>
        /// divided by <see cref="TimedReprocessFrequency"/> so that the reprocessor does not get behind.
        /// </summary>
        public int TimedReprocessMaxBuckets { get; set; } = 50;

        public bool AutoStartCatalogScanUpdate { get; set; } = false;
        public bool AutoStartDownloadToCsv { get; set; } = false;
        public bool AutoStartOwnersToCsv { get; set; } = false;
        public bool AutoStartVerifiedPackagesToCsv { get; set; } = false;
        public bool AutoStartExcludedPackagesToCsv { get; set; } = false;
        public bool AutoStartPopularityTransfersToCsv { get; set; } = false;
        public bool AutoStartTimedReprocess { get; set; } = false;

        public TimeSpan CatalogScanUpdateFrequency { get; set; } = TimeSpan.FromHours(6);
        public TimeSpan DownloadToCsvFrequency { get; set; } = TimeSpan.FromHours(3);
        public TimeSpan OwnersToCsvFrequency { get; set; } = TimeSpan.FromHours(3);
        public TimeSpan VerifiedPackagesToCsvFrequency { get; set; } = TimeSpan.FromHours(3);
        public TimeSpan ExcludedPackagesToCsvFrequency { get; set; } = TimeSpan.FromHours(3);
        public TimeSpan PopularityTransfersToCsvFrequency { get; set; } = TimeSpan.FromHours(3);
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

        public string WorkQueueName { get; set; } = "work";
        public string ExpandQueueName { get; set; } = "expand";
        public string CursorTableName { get; set; } = "cursors";
        public string CatalogIndexScanTableName { get; set; } = "catalogindexscans";
        public string CatalogPageScanTableName { get; set; } = "catalogpagescans";
        public string CatalogLeafScanTableName { get; set; } = "catalogleafscans";
        public string TaskStateTableName { get; set; } = "taskstate";
        public string CsvRecordTableName { get; set; } = "csvrecords";
        public string KustoIngestionTableName { get; set; } = "kustoingestions";
        public string WorkflowRunTableName { get; set; } = "workflowruns";
        public string TimedReprocessTableName { get; set; } = "timedreprocess";

        public string PackageDownloadContainerName { get; set; } = "packagedownloads";
        public string PackageOwnerContainerName { get; set; } = "packageowners";
        public string VerifiedPackageContainerName { get; set; } = "verifiedpackages";
        public string ExcludedPackageContainerName { get; set; } = "excludedpackages";
        public string PopularityTransferContainerName { get; set; } = "popularitytransfers";

        public string KustoConnectionString { get; set; } = null;
        public string KustoDatabaseName { get; set; } = null;
        public string KustoClientCertificateKeyVault { get; set; } = null;
        public string KustoClientCertificateKeyVaultCertificateName { get; set; } = null;
        public bool KustoUseUserManagedIdentity { get; set; } = true;

        /// <summary>
        /// A path to a certificate that will be loaded as a <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2"/>
        /// for Kusto AAD client app authentication.
        /// </summary>
        public string KustoClientCertificatePath { get; set; } = null;

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
