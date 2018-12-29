using System.IO;

namespace Knapcode.ExplorePackages.Logic
{
    public class ExplorePackagesSettings
    {
        public ExplorePackagesSettings()
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            DatabaseType = DatabaseType.Sqlite;
            DatabaseConnectionString = "Data Source=" + Path.Combine(currentDirectory, "Knapcode.ExplorePackages.sqlite3");
            PackagePath = Path.Combine(currentDirectory, "packages");
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
        }

        public DatabaseType DatabaseType { get; set; }
        public string DatabaseConnectionString { get; set; }
        public string PackagePath { get; set; }
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
    }
}
