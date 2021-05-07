using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker
{
    public class ExplorePackagesWorkerSettings : ExplorePackagesSettings
    {
        public ExplorePackagesWorkerSettings()
        {
            UseBulkEnqueueStrategy = true;
            BulkEnqueueThreshold = 10;
            EnqueueWorkers = 1;
            AppendResultStorageBucketCount = 1000; // Azure Data Explorer can only import up to 1000 blobs.
            AllowBatching = true;
            RunAllCatalogScanDriversAsBatch = false;
            OnlyKeepLatestInStreamWriterUpdater = true;
            MoveTempToHome = false;
            DisabledDrivers = new List<CatalogScanDriverType>();
            OldCatalogIndexScansToKeep = 9;

            AutoStartCatalogScanUpdate = false;
            AutoStartDownloadToCsv = false;
            AutoStartOwnersToCsv = false;
            CatalogScanUpdateFrequency = TimeSpan.FromHours(6);
            DownloadToCsvFrequency = TimeSpan.FromHours(3);
            OwnersToCsvFrequency = TimeSpan.FromHours(3);

            HostSubscriptionId = null;
            HostResourceGroupName = null;
            HostAppName = null;

            WorkQueueName = "work";
            ExpandQueueName = "expand";
            CursorTableName = "cursors";
            CatalogIndexScanTableName = "catalogindexscans";
            CatalogPageScanTableName = "catalogpagescans";
            CatalogLeafScanTableName = "catalogleafscans";
            TaskStateTableName = "taskstate";
            CsvRecordTableName = "csvrecords";
            VersionSetAggregateTableName = "versionset";
            VersionSetContainerName = "versionset";
            KustoIngestionTableName = "kustoingestions";

            LatestPackageLeafTableName = "latestpackageleaves";
            PackageAssetContainerName = "packageassets";
            PackageAssemblyContainerName = "packageassemblies";
            PackageManifestContainerName = "packagemanifests";
            PackageSignatureContainerName = "packagesignatures";
            RealRestoreContainerName = "realrestores";
            CatalogLeafItemContainerName = "catalogleafitems";
            PackageDownloadsContainerName = "packagedownloads";
            PackageOwnersContainerName = "packageowners";
            PackageArchiveContainerName = "packagearchives";
            PackageArchiveEntryContainerName = "packagearchiveentries";
            PackageVersionTableName = "packageversions";
            PackageVersionContainerName = "packageversions";
            NuGetPackageExplorerContainerName = "nugetpackageexplorer";
            NuGetPackageExplorerFileContainerName = "nugetpackageexplorerfiles";

            KustoConnectionString = null;
            KustoDatabaseName = null;
            KustoTableNameFormat = "{0}";
            OldKustoIngestionsToKeep = 9;
        }

        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public int AppendResultStorageBucketCount { get; set; }
        public bool AllowBatching { get; set; }
        public bool RunAllCatalogScanDriversAsBatch { get; set; }
        public bool OnlyKeepLatestInStreamWriterUpdater { get; set; }
        public bool MoveTempToHome { get; set; }
        public List<CatalogScanDriverType> DisabledDrivers { get; set; }
        public int OldCatalogIndexScansToKeep { get; set; }

        public bool AutoStartCatalogScanUpdate { get; set; }
        public bool AutoStartDownloadToCsv { get; set; }
        public bool AutoStartOwnersToCsv { get; set; }
        public TimeSpan CatalogScanUpdateFrequency { get; set; }
        public TimeSpan DownloadToCsvFrequency { get; set; }
        public TimeSpan OwnersToCsvFrequency { get; set; }

        public string HostSubscriptionId { get; set; }
        public string HostResourceGroupName { get; set; }
        public string HostAppName { get; set; }

        public string WorkQueueName { get; set; }
        public string ExpandQueueName { get; set; }
        public string CursorTableName { get; set; }
        public string CatalogIndexScanTableName { get; set; }
        public string CatalogPageScanTableName { get; set; }
        public string CatalogLeafScanTableName { get; set; }
        public string TaskStateTableName { get; set; }
        public string CsvRecordTableName { get; set; }
        public string VersionSetAggregateTableName { get; set; }
        public string VersionSetContainerName { get; set; }
        public string KustoIngestionTableName { get; set; }

        public string LatestPackageLeafTableName { get; set; }
        public string PackageAssetContainerName { get; set; }
        public string PackageAssemblyContainerName { get; set; }
        public string PackageManifestContainerName { get; set; }
        public string PackageSignatureContainerName { get; set; }
        public string RealRestoreContainerName { get; set; }
        public string CatalogLeafItemContainerName { get; set; }
        public string PackageDownloadsContainerName { get; set; }
        public string PackageOwnersContainerName { get; set; }
        public string PackageArchiveContainerName { get; set; }
        public string PackageArchiveEntryContainerName { get; set; }
        public string PackageVersionTableName { get; set; }
        public string PackageVersionContainerName { get; set; }
        public string NuGetPackageExplorerContainerName { get; set; }
        public string NuGetPackageExplorerFileContainerName { get; set; }

        public string KustoConnectionString { get; set; }
        public string KustoDatabaseName { get; set; }
        public string KustoTableNameFormat { get; set; }
        public int OldKustoIngestionsToKeep { get; set; }
    }
}
