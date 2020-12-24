using System.Collections.Generic;
using System.IO;

namespace Knapcode.ExplorePackages
{
    public class ExplorePackagesSettings
    {
        public const string DefaultSectionName = "Knapcode.ExplorePackages";

        public ExplorePackagesSettings()
        {
            GalleryBaseUrl = "https://www.nuget.org";
            PackagesContainerBaseUrl = "https://globalcdn.nuget.org/packages";
            V2BaseUrl = "https://www.nuget.org/api/v2";
            V3ServiceIndex = "https://api.nuget.org/v3/index.json";
            DownloadsV1Url = null;
            StorageConnectionString = "UseDevelopmentStorage=true";
            StorageContainerName = "packages";
            LeaseContainerName = "leases";
            IsStorageContainerPublic = false;
            MaxTempMemoryStreamSize = 1024 * 1024 * 255;
            TempDirectories = new List<TempStreamDirectory>
            {
                Path.Combine(Path.GetTempPath(), "Knapcode.ExplorePackages"),
            };
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
        public int MaxTempMemoryStreamSize { get; set; }
        public List<TempStreamDirectory> TempDirectories { get; set; }
    }
}
