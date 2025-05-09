// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.FileSystemHttpCache;

namespace NuGet.Insights
{
    public class NuGetInsightsSettings : StorageSettings
    {
        public const string DefaultSectionName = "NuGetInsights";

        public string DeploymentLabel { get; set; } = null;
        public string GalleryBaseUrl { get; set; } = "https://www.nuget.org";
        public string PackagesContainerBaseUrl { get; set; } = "https://globalcdn.nuget.org/packages";
        public string SymbolPackagesContainerBaseUrl { get; set; } = "https://globalcdn.nuget.org/symbol-packages";
        public string V2BaseUrl { get; set; } = "https://www.nuget.org/api/v2";
        public string V3ServiceIndex { get; set; } = "https://api.nuget.org/v3/index.json";
        public string FlatContainerBaseUrlOverride { get; set; } = null;

        /// <summary>
        /// Whether or not to use an authenticated Azure Storage blob client to fetch the auxiliary file URLs
        /// configured in <see cref="DownloadsV1Urls"/>, <see cref="DownloadsV2Urls"/>, etc. If left as <c>null</c>,
        /// the blob client will default to being used if a storage token credential is used, or if the base URL of the
        /// external URL matches the base URL of the configured storage account.
        /// </summary>
        public bool? UseBlobClientForExternalData { get; set; } = null;

        public List<string> DownloadsV1Urls { get; set; } = new List<string>();
        public TimeSpan DownloadsV1AgeLimit { get; set; } = TimeSpan.FromDays(7);
        public List<string> DownloadsV2Urls { get; set; } = new List<string>();
        public TimeSpan DownloadsV2AgeLimit { get; set; } = TimeSpan.FromDays(7);
        public List<string> OwnersV2Urls { get; set; } = new List<string>();
        public TimeSpan OwnersV2AgeLimit { get; set; } = TimeSpan.FromDays(1);
        public List<string> VerifiedPackagesV1Urls { get; set; } = new List<string>();
        public TimeSpan VerifiedPackagesV1AgeLimit { get; set; } = TimeSpan.FromDays(7);
        public List<string> ExcludedPackagesV1Urls { get; set; } = new List<string>();
        public TimeSpan ExcludedPackagesV1AgeLimit { get; set; } = TimeSpan.FromDays(365 * 10);
        public List<string> PopularityTransfersV1Urls { get; set; } = new List<string>();
        public TimeSpan PopularityTransfersV1AgeLimit { get; set; } = TimeSpan.FromDays(365 * 10);
        public List<string> GitHubUsageV1Urls { get; set; } = new List<string>();
        public TimeSpan GitHubUsageV1AgeLimit { get; set; } = TimeSpan.FromDays(7);
        public string LegacyReadmeUrlPattern { get; set; } = null;

        public string LeaseContainerName { get; set; } = "leases";
        public string PackageArchiveTableName { get; set; } = "packagearchives";
        public string SymbolPackageArchiveTableName { get; set; } = "symbolpackagearchives";
        public string PackageManifestTableName { get; set; } = "packagemanifests";
        public string PackageReadmeTableName { get; set; } = "packagereadmes";
        public string PackageHashesTableName { get; set; } = "packagehashes";
        public string SymbolPackageHashesTableName { get; set; } = "symbolpackagehashes";
        public string TimerTableName { get; set; } = "timers";

        public int MaxTempMemoryStreamSize { get; set; } = 1024 * 1024 * 196;

        public List<TempStreamDirectory> TempDirectories { get; set; } = new List<TempStreamDirectory>
        {
            Path.Combine(Path.GetTempPath(), "NuGet.Insights"),
        };

        public TimeSpan HttpClientNetworkTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public int HttpClientMaxRetries { get; set; } = 4;
        public TimeSpan HttpClientMaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
        public bool HttpClientAddRetryJitter { get; set; } = true;
        public string HttpCacheDirectory { get; set; } = null;
        public FileSystemHttpCacheMode HttpCacheMode { get; set; } = FileSystemHttpCacheMode.Disabled;
    }
}
