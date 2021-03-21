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
            AppendResultUniqueIds = true;
            AllowBatching = true;
            RunAllCatalogScanDriversAsBatch = false;
            OnlyKeepLatestInStreamWriterUpdater = true;
            MoveTempToHome = false;
            DisabledDrivers = new List<CatalogScanDriverType>();

            AutoStartCatalogScanUpdate = false;
            AutoStartDownloadToCsv = false;
            AutoStartOwnersToCsv = false;
            CatalogScanUpdateFrequency = TimeSpan.FromHours(6);
            DownloadToCsvFrequency = TimeSpan.FromHours(3);
            OwnersToCsvFrequency = TimeSpan.FromHours(3);

            WorkerQueueName = "workerqueue";
            CursorTableName = "cursors";
            CatalogIndexScanTableName = "catalogindexscans";
            CatalogPageScanTableName = "catalogpagescans";
            CatalogLeafScanTableName = "catalogleafscans";
            TaskStateTableName = "taskstate";
            CsvRecordTableName = "csvrecords";
            VersionSetAggregateTableName = "versionset";
            VersionSetContainerName = "versionset";

            LatestPackageLeafTableName = "latestpackageleaves";
            PackageAssetContainerName = "packageassets";
            PackageAssemblyContainerName = "packageassemblies";
            PackageManifestContainerName = "packagemanifests";
            PackageSignatureContainerName = "packagesignatures";
            RealRestoreContainerName = "realrestores";
            CatalogLeafItemContainerName = "catalogleafitems";
            PackageDownloadsContainerName = "packagedownloads";
            PackageOwnersContainerName = "packageowners";
            PackageArchiveEntryContainerName = "packagearchiveentries";
            PackageVersionTableName = "packageversions";
            PackageVersionContainerName = "packageversions";
            NuGetPackageExplorerContainerName = "nugetpackageexplorer";
        }

        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public int AppendResultStorageBucketCount { get; set; }
        public bool AppendResultUniqueIds { get; set; }
        public bool AllowBatching { get; set; }
        public bool RunAllCatalogScanDriversAsBatch { get; set; }
        public bool OnlyKeepLatestInStreamWriterUpdater { get; set; }
        public bool MoveTempToHome { get; set; }
        public List<CatalogScanDriverType> DisabledDrivers { get; set; }

        public bool AutoStartCatalogScanUpdate { get; set; }
        public bool AutoStartDownloadToCsv { get; set; }
        public bool AutoStartOwnersToCsv { get; set; }
        public TimeSpan CatalogScanUpdateFrequency { get; set; }
        public TimeSpan DownloadToCsvFrequency { get; set; }
        public TimeSpan OwnersToCsvFrequency { get; set; }

        public string WorkerQueueName { get; set; }
        public string CursorTableName { get; set; }
        public string CatalogIndexScanTableName { get; set; }
        public string CatalogPageScanTableName { get; set; }
        public string CatalogLeafScanTableName { get; set; }
        public string TaskStateTableName { get; set; }
        public string CsvRecordTableName { get; set; }
        public string VersionSetAggregateTableName { get; set; }
        public string VersionSetContainerName { get; set; }

        public string LatestPackageLeafTableName { get; set; }
        public string PackageAssetContainerName { get; set; }
        public string PackageAssemblyContainerName { get; set; }
        public string PackageManifestContainerName { get; set; }
        public string PackageSignatureContainerName { get; set; }
        public string RealRestoreContainerName { get; set; }
        public string CatalogLeafItemContainerName { get; set; }
        public string PackageDownloadsContainerName { get; set; }
        public string PackageOwnersContainerName { get; set; }
        public string PackageArchiveEntryContainerName { get; set; }
        public string PackageVersionTableName { get; set; }
        public string PackageVersionContainerName { get; set; }
        public string NuGetPackageExplorerContainerName { get; set; }
    }
}
