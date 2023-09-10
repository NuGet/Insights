// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            SymbolPackagesContainerBaseUrl = "https://globalcdn.nuget.org/symbol-packages";
            V2BaseUrl = "https://www.nuget.org/api/v2";
            V3ServiceIndex = "https://api.nuget.org/v3/index.json";
            FlatContainerBaseUrlOverride = null;
            DownloadsV1Urls = new List<string>();
            DownloadsV1AgeLimit = TimeSpan.FromDays(7);
            OwnersV2Urls = new List<string>();
            OwnersV2AgeLimit = TimeSpan.FromDays(1);
            VerifiedPackagesV1Urls = new List<string>();
            VerifiedPackagesV1AgeLimit = TimeSpan.FromDays(7);
            LegacyReadmeUrlPattern = null;
            StorageAccountName = null;
            StorageBlobReadSharedAccessSignature = null;
            StorageConnectionString = StorageUtility.EmulatorConnectionString;
            ServiceClientRefreshPeriod = TimeSpan.FromMinutes(30);
            ServiceClientSasDuration = TimeSpan.FromHours(12);
            LeaseContainerName = "leases";
            PackageArchiveTableName = "packagearchives";
            SymbolPackageArchiveTableName = "symbolpackagearchives";
            PackageManifestTableName = "packagemanifests";
            PackageReadmeTableName = "packagereadmes";
            PackageHashesTableName = "packagehashes";
            TimerTableName = "timers";
            MaxTempMemoryStreamSize = 1024 * 1024 * 196;
            UserManagedIdentityClientId = null;
            TempDirectories = new List<TempStreamDirectory>
            {
                Path.Combine(Path.GetTempPath(), "NuGet.Insights"),
            };
        }

        public string GalleryBaseUrl { get; set; }
        public string PackagesContainerBaseUrl { get; set; }
        public string SymbolPackagesContainerBaseUrl { get; set; }
        public string V2BaseUrl { get; set; }
        public string V3ServiceIndex { get; set; }
        public string FlatContainerBaseUrlOverride { get; set; }
        public List<string> DownloadsV1Urls { get; set; }
        public TimeSpan DownloadsV1AgeLimit { get; set; }
        public List<string> OwnersV2Urls { get; set; }
        public TimeSpan OwnersV2AgeLimit { get; set; }
        public List<string> VerifiedPackagesV1Urls { get; set; }
        public TimeSpan VerifiedPackagesV1AgeLimit { get; set; }
        public string LegacyReadmeUrlPattern { get; set; }

        public string StorageAccountName { get; set; }
        public string StorageBlobReadSharedAccessSignature { get; set; }
        public string StorageConnectionString { get; set; }
        public TimeSpan ServiceClientRefreshPeriod { get; set; }
        public TimeSpan ServiceClientSasDuration { get; set; }

        public string LeaseContainerName { get; set; }
        public string PackageArchiveTableName { get; set; }
        public string SymbolPackageArchiveTableName { get; set; }
        public string PackageManifestTableName { get; set; }
        public string PackageReadmeTableName { get; set; }
        public string PackageHashesTableName { get; set; }
        public string TimerTableName { get; set; }
        public int MaxTempMemoryStreamSize { get; set; }
        public string UserManagedIdentityClientId { get; set; }
        public List<TempStreamDirectory> TempDirectories { get; set; }
    }
}
