namespace Knapcode.ExplorePackages.Worker
{
    public class ExplorePackagesWorkerSettings : ExplorePackagesSettings
    {
        public ExplorePackagesWorkerSettings()
        {
            UseBulkEnqueueStrategy = true;
            BulkEnqueueThreshold = 10;
            EnqueueWorkers = 1;
            WorkerQueueName = "worker-queue";
            AppendResultStorageMode = AppendResultStorageMode.Table;
            AppendResultStorageBucketCount = 1000; // Azure Data Explorer can only import up to 1000 blobs.
            AppendResultUniqueIds = true;
        }

        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public string WorkerQueueName { get; set; }
        public AppendResultStorageMode AppendResultStorageMode { get; set; }
        public int AppendResultStorageBucketCount { get; set; }
        public bool AppendResultUniqueIds { get; set; }
    }
}
