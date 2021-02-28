namespace Knapcode.ExplorePackages.Worker
{
    public class ExplorePackagesWorkerSettings : ExplorePackagesSettings
    {
        public ExplorePackagesWorkerSettings()
        {
            UseBulkEnqueueStrategy = true;
            BulkEnqueueThreshold = 10;
            EnqueueWorkers = 1;
            AppendResultStorageMode = AppendResultStorageMode.Table;
            AppendResultStorageBucketCount = 1000; // Azure Data Explorer can only import up to 1000 blobs.
            AppendResultUniqueIds = true;
            AllowBatching = true;
            RunAllCatalogScanDriversAsBatch = false;
            OnlyKeepLatestInStreamWriterUpdater = true;

            WorkerQueueName = "workerqueue";
            CursorTableName = "cursors";
            CatalogIndexScanTableName = "catalogindexscans";
            CatalogPageScanTableName = "catalogpagescans";
            CatalogLeafScanTableName = "catalogleafscans";
            TaskStateTableName = "taskstate";
            CsvRecordTableName = "csvrecords";

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
        }

        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public AppendResultStorageMode AppendResultStorageMode { get; set; }
        public int AppendResultStorageBucketCount { get; set; }
        public bool AppendResultUniqueIds { get; set; }
        public bool AllowBatching { get; set; }
        public bool RunAllCatalogScanDriversAsBatch { get; set; }
        public bool OnlyKeepLatestInStreamWriterUpdater { get; set; }

        public string WorkerQueueName { get; set; }
        public string CursorTableName { get; set; }
        public string CatalogIndexScanTableName { get; set; }
        public string CatalogPageScanTableName { get; set; }
        public string CatalogLeafScanTableName { get; set; }
        public string TaskStateTableName { get; set; }
        public object CsvRecordTableName { get; set; }

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
    }
}
