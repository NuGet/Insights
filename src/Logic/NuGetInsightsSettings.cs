using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Insights
{
    public class NuGetInsightsSettings
    {
        public const string DefaultSectionName = "NuGet.Insights";

        public NuGetInsightsSettings()
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
            StorageBlobReadSharedAccessSignature = null;
            KeyVaultName = null;
            StorageSharedAccessSignatureSecretName = null;
            StorageSharedAccessSignatureDuration = null;
            StorageConnectionString = StorageUtility.EmulatorConnectionString;
            StorageConnectionStringSecretName = null;
            StorageContainerName = "packages";
            LeaseContainerName = "leases";
            PackageArchiveTableName = "packagearchives";
            PackageManifestTableName = "packagemanifests";
            PackageHashesTableName = "packagehashes";
            TimerTableName = "timers";
            IsStorageContainerPublic = false;
            MaxTempMemoryStreamSize = 1024 * 1024 * 196;
            UserManagedIdentityClientId = null;
            EnableAzureLogging = false;
            TempDirectories = new List<TempStreamDirectory>
            {
                Path.Combine(Path.GetTempPath(), "NuGet.Insights"),
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
        public string StorageBlobReadSharedAccessSignature { get; set; }
        public string KeyVaultName { get; set; }
        public string StorageSharedAccessSignatureSecretName { get; set; }
        public string StorageBlobReadSharedAccessSignatureSecretName { get; set; }
        public TimeSpan? StorageSharedAccessSignatureDuration { get; set; }
        public string StorageConnectionString { get; set; }
        public string StorageConnectionStringSecretName { get; set; }
        public string StorageContainerName { get; set; }
        public string LeaseContainerName { get; set; }
        public string PackageArchiveTableName { get; set; }
        public string PackageManifestTableName { get; set; }
        public string PackageHashesTableName { get; set; }
        public string TimerTableName { get; set; }
        public bool IsStorageContainerPublic { get; set; }
        public int MaxTempMemoryStreamSize { get; set; }
        public string UserManagedIdentityClientId { get; set; }
        public bool EnableAzureLogging { get; set; }
        public List<TempStreamDirectory> TempDirectories { get; set; }
    }
}
