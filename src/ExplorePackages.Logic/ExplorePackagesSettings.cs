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
            FlatContainerBaseUrlOverride = null;
            DownloadsV1Url = null;
            OwnersV2Url = null;
            StorageAccountName = null;
            StorageSharedAccessSignature = null;
            KeyVaultName = null;
            StorageSharedAccessSignatureSecretName = null;
            StorageConnectionString = StorageUtility.EmulatorConnectionString;
            StorageConnectionStringSecretName = null;
            StorageContainerName = "packages";
            LeaseContainerName = "leases";
            PackageArchiveTableName = "packagearchives";
            PackageManifestTableName = "packagemanifests";
            TimerTableName = "timers";
            IsStorageContainerPublic = false;
            MaxTempMemoryStreamSize = 1024 * 1024 * 196;
            TempDirectories = new List<TempStreamDirectory>
            {
                Path.Combine(Path.GetTempPath(), "Knapcode.ExplorePackages"),
            };
        }

        public string GalleryBaseUrl { get; set; }
        public string PackagesContainerBaseUrl { get; set; }
        public string V2BaseUrl { get; set; }
        public string V3ServiceIndex { get; set; }
        public string FlatContainerBaseUrlOverride { get; set; }
        public string DownloadsV1Url { get; set; }
        public string OwnersV2Url { get; set; }
        public object StorageAccountName { get; set; }
        public string StorageSharedAccessSignature { get; set; }
        public string KeyVaultName { get; set; }
        public string StorageSharedAccessSignatureSecretName { get; set; }
        public string StorageConnectionString { get; set; }
        public string StorageConnectionStringSecretName { get; set; }
        public string StorageContainerName { get; set; }
        public string LeaseContainerName { get; set; }
        public string PackageArchiveTableName { get; set; }
        public string PackageManifestTableName { get; set; }
        public string TimerTableName { get; set; }
        public bool IsStorageContainerPublic { get; set; }
        public int MaxTempMemoryStreamSize { get; set; }
        public List<TempStreamDirectory> TempDirectories { get; set; }
    }
}
