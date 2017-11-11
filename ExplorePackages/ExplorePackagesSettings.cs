using System.IO;

namespace Knapcode.ExplorePackages
{
    public class ExplorePackagesSettings
    {
        public ExplorePackagesSettings()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            DatabasePath = Path.Combine(currentDirectory, "Knapcode.ExplorePackages.sqlite3");
            PackagePath = Path.Combine(currentDirectory, "packages");
            RunConsistencyChecks = true;
            GalleryBaseUrl = "https://www.nuget.org";
            PackagesContainerBaseUrl = "https://api.nuget.org/packages";
            V2BaseUrl = "https://www.nuget.org/api/v2";
            V3ServiceIndex = "https://api.nuget.org/v3/index.json";
        }

        public string DatabasePath { get; set; }
        public string PackagePath { get; set; }
        public bool RunConsistencyChecks { get; set; }
        public string GalleryBaseUrl { get; set; }
        public string PackagesContainerBaseUrl { get; set; }
        public string V2BaseUrl { get; set; }
        public string V3ServiceIndex { get; set; }

        public ExplorePackagesSettings Clone()
        {
            return new ExplorePackagesSettings
            {
                DatabasePath = DatabasePath,
                PackagePath = PackagePath,
                RunConsistencyChecks = RunConsistencyChecks,
                GalleryBaseUrl = GalleryBaseUrl,
                PackagesContainerBaseUrl = PackagesContainerBaseUrl,
                V2BaseUrl = V2BaseUrl,
                V3ServiceIndex = V3ServiceIndex,
            };
        }
    }
}
