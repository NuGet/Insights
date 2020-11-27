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
        }

        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public string WorkerQueueName { get; set; }
        public AppendResultStorageMode AppendResultStorageMode { get; set; }
    }
}
