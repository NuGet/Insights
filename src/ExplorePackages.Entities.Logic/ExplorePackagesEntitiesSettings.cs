using System.IO;

namespace Knapcode.ExplorePackages.Entities
{
    public class ExplorePackagesEntitiesSettings : ExplorePackagesSettings
    {
        public ExplorePackagesEntitiesSettings()
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            DatabaseType = DatabaseType.Sqlite;
            DatabaseConnectionString = "Data Source=" + Path.Combine(currentDirectory, "Knapcode.ExplorePackages.sqlite3");
            RunConsistencyChecks = true;
            RunBoringQueries = false;
            DownloadsV1Path = Path.Combine(currentDirectory, "downloads.txt");
            WorkerCount = 32;
        }

        public DatabaseType DatabaseType { get; set; }
        public string DatabaseConnectionString { get; set; }
        public bool RunConsistencyChecks { get; set; }
        public bool RunBoringQueries { get; set; }
        public int WorkerCount { get; set; }
        public string DownloadsV1Path { get; set; }
    }
}
