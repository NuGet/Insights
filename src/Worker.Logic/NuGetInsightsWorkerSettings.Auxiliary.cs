// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings
    {
        public bool AutoStartDownloadToCsv { get; set; } = false;
        public bool AutoStartOwnersToCsv { get; set; } = false;
        public bool AutoStartVerifiedPackagesToCsv { get; set; } = false;
        public bool AutoStartExcludedPackagesToCsv { get; set; } = false;
        public bool AutoStartPopularityTransfersToCsv { get; set; } = false;
        public bool AutoStartGitHubUsageToCsv { get; set; } = false;

        public string DownloadToCsvFrequency { get; set; } = TimeSpan.FromHours(3).ToString();
        public string OwnersToCsvFrequency { get; set; } = TimeSpan.FromHours(3).ToString();
        public string VerifiedPackagesToCsvFrequency { get; set; } = TimeSpan.FromHours(3).ToString();
        public string ExcludedPackagesToCsvFrequency { get; set; } = TimeSpan.FromHours(3).ToString();
        public string PopularityTransfersToCsvFrequency { get; set; } = TimeSpan.FromHours(3).ToString();
        public string GitHubUsageToCsvFrequency { get; set; } = TimeSpan.FromHours(3).ToString();

        public string PackageDownloadContainerName { get; set; } = "packagedownloads";
        public string PackageOwnerContainerName { get; set; } = "packageowners";
        public string VerifiedPackageContainerName { get; set; } = "verifiedpackages";
        public string ExcludedPackageContainerName { get; set; } = "excludedpackages";
        public string PopularityTransferContainerName { get; set; } = "popularitytransfers";
        public string GitHubUsageContainerName { get; set; } = "githubusage";
    }
}
