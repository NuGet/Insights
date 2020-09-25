using System.IO;

namespace Knapcode.ExplorePackages.Logic
{
    public class ExplorePackagesSettings
    {
        public const string DefaultSectionName = "Knapcode.ExplorePackages";

        public ExplorePackagesSettings()
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            DatabaseType = DatabaseType.Sqlite;
            DatabaseConnectionString = "Data Source=" + Path.Combine(currentDirectory, "Knapcode.ExplorePackages.sqlite3");
            RunConsistencyChecks = true;
            RunBoringQueries = false;
            GalleryBaseUrl = "https://www.nuget.org";
            PackagesContainerBaseUrl = "https://api.nuget.org/packages";
            V2BaseUrl = "https://www.nuget.org/api/v2";
            V3ServiceIndex = "https://api.nuget.org/v3/index.json";
            DownloadsV1Url = null;
            DownloadsV1Path = Path.Combine(currentDirectory, "downloads.txt");
            StorageConnectionString = "UseDevelopmentStorage=true";
            StorageContainerName = "packages";
            IsStorageContainerPublic = false;
            WorkerCount = 32;
            LatestPackageLeavesStorageConnectionString = "UseDevelopmentStorage=true";
            UseBulkEnqueueStrategy = false;
            BulkEnqueueThreshold = 1;
            EnqueueWorkers = 1;
            WorkerQueueName = "queue";
        }

        public DatabaseType DatabaseType { get; set; }
        public string DatabaseConnectionString { get; set; }
        public bool RunConsistencyChecks { get; set; }
        public bool RunBoringQueries { get; set; }
        public string GalleryBaseUrl { get; set; }
        public string PackagesContainerBaseUrl { get; set; }
        public string V2BaseUrl { get; set; }
        public string V3ServiceIndex { get; set; }
        public string DownloadsV1Url { get; set; }
        public string DownloadsV1Path { get; set; }
        public string StorageConnectionString { get; set; }
        public string StorageContainerName { get; set; }
        public bool IsStorageContainerPublic { get; set; }
        public int WorkerCount { get; set; }
        public string ServiceBusConnectionString { get; set; }
        public string LatestPackageLeavesStorageConnectionString { get; set; }
        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public string WorkerQueueName { get; set; }

        public string FindPackageAssetsSqlConnectionString { get; set; }
    }
}
