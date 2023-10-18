// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Insights
{
    public class NuGetInsightsSettings
    {
        public const string DefaultSectionName = "NuGetInsights";

        public string DeploymentLabel { get; set; } = null;
        public string GalleryBaseUrl { get; set; } = "https://www.nuget.org";
        public string PackagesContainerBaseUrl { get; set; } = "https://globalcdn.nuget.org/packages";
        public string SymbolPackagesContainerBaseUrl { get; set; } = "https://globalcdn.nuget.org/symbol-packages";
        public string V2BaseUrl { get; set; } = "https://www.nuget.org/api/v2";
        public string V3ServiceIndex { get; set; } = "https://api.nuget.org/v3/index.json";
        public string FlatContainerBaseUrlOverride { get; set; } = null;
        public List<string> DownloadsV1Urls { get; set; } = new List<string>();
        public TimeSpan DownloadsV1AgeLimit { get; set; } = TimeSpan.FromDays(7);
        public List<string> OwnersV2Urls { get; set; } = new List<string>();
        public TimeSpan OwnersV2AgeLimit { get; set; } = TimeSpan.FromDays(1);
        public List<string> VerifiedPackagesV1Urls { get; set; } = new List<string>();
        public TimeSpan VerifiedPackagesV1AgeLimit { get; set; } = TimeSpan.FromDays(7);
        public string LegacyReadmeUrlPattern { get; set; } = null;

        public string StorageAccountName { get; set; } = null;
        public string StorageBlobReadSharedAccessSignature { get; set; } = null;
        public string StorageConnectionString { get; set; } = StorageUtility.EmulatorConnectionString;
        public TimeSpan ServiceClientRefreshPeriod { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan ServiceClientSasDuration { get; set; } = TimeSpan.FromHours(12);

        public string LeaseContainerName { get; set; } = "leases";
        public string PackageArchiveTableName { get; set; } = "packagearchives";
        public string SymbolPackageArchiveTableName { get; set; } = "symbolpackagearchives";
        public string PackageManifestTableName { get; set; } = "packagemanifests";
        public string PackageReadmeTableName { get; set; } = "packagereadmes";
        public string PackageHashesTableName { get; set; } = "packagehashes";
        public string TimerTableName { get; set; } = "timers";
        public int MaxTempMemoryStreamSize { get; set; } = 1024 * 1024 * 196;
        public string UserManagedIdentityClientId { get; set; } = null;
        public List<TempStreamDirectory> TempDirectories { get; set; } = new List<TempStreamDirectory>
        {
            Path.Combine(Path.GetTempPath(), "NuGet.Insights"),
        };
    }
}
