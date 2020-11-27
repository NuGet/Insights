using Knapcode.ExplorePackages.Logic.Worker;

namespace Knapcode.ExplorePackages.Logic
{
    public class ExplorePackagesSettings
    {
        public const string DefaultSectionName = "Knapcode.ExplorePackages";

        public ExplorePackagesSettings()
        {
            GalleryBaseUrl = "https://www.nuget.org";
            PackagesContainerBaseUrl = "https://api.nuget.org/packages";
            V2BaseUrl = "https://www.nuget.org/api/v2";
            V3ServiceIndex = "https://api.nuget.org/v3/index.json";
            DownloadsV1Url = null;
            StorageConnectionString = "UseDevelopmentStorage=true";
            StorageContainerName = "packages";
            LeaseContainerName = "leases";
            IsStorageContainerPublic = false;
            UseBulkEnqueueStrategy = true;
            BulkEnqueueThreshold = 10;
            EnqueueWorkers = 1;
            WorkerQueueName = "worker-queue";
            AppendResultStorageMode = AppendResultStorageMode.Table;
        }

        public string GalleryBaseUrl { get; set; }
        public string PackagesContainerBaseUrl { get; set; }
        public string V2BaseUrl { get; set; }
        public string V3ServiceIndex { get; set; }
        public string DownloadsV1Url { get; set; }
        public string StorageConnectionString { get; set; }
        public string StorageContainerName { get; set; }
        public string LeaseContainerName { get; set; }
        public bool IsStorageContainerPublic { get; set; }
        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public string WorkerQueueName { get; set; }
        public AppendResultStorageMode AppendResultStorageMode { get; set; }
    }
}
