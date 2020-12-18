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
            AppendResultStorageMode = AppendResultStorageMode.Table;
            AppendResultStorageBucketCount = 1000; // Azure Data Explorer can only import up to 1000 blobs.
            AppendResultUniqueIds = true;
            MessageBatchSizes = new Dictionary<string, int>();

            WorkerQueueName = "workerqueue";
            CursorTableName = "cursors";
            CatalogIndexScanTableName = "catalogindexscans";
            CatalogPageScanTableName = "catalogpagescans";
            CatalogLeafScanTableName = "catalogleafscans";
            TaskStateTableName = "taskstate";
            LatestLeavesTableName = "latestleaves";
            FindPackageAssetsContainerName = "findpackageassets";
            RunRealRestoreContainerName = "runrealrestore";
        }

        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public AppendResultStorageMode AppendResultStorageMode { get; set; }
        public int AppendResultStorageBucketCount { get; set; }
        public bool AppendResultUniqueIds { get; set; }
        public Dictionary<string, int> MessageBatchSizes { get; set; }

        public string WorkerQueueName { get; set; }
        public string CursorTableName { get; set; }
        public string CatalogIndexScanTableName { get; set; }
        public string CatalogPageScanTableName { get; set; }
        public string CatalogLeafScanTableName { get; set; }
        public string TaskStateTableName { get; set; }
        public string LatestLeavesTableName { get; set; }
        public string FindPackageAssetsContainerName { get; set; }
        public string RunRealRestoreContainerName { get; set; }
    }
}
