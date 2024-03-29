﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class SearchConsistencyService : IConsistencyService<SearchConsistencyReport>
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly SearchClient _searchClient;
        private readonly ILogger<SearchConsistencyService> _logger;

        public SearchConsistencyService(
            ServiceIndexCache serviceIndexCache,
            SearchClient searchClient,
            ILogger<SearchConsistencyService> logger)
        {
            _serviceIndexCache = serviceIndexCache;
            _searchClient = searchClient;
            _logger = logger;
        }

        public async Task<SearchConsistencyReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter, allowPartial: false);
            return new SearchConsistencyReport(
                report.IsConsistent,
                report.BaseUrlHasPackageSemVer1,
                report.BaseUrlHasPackageSemVer2,
                report.BaseUrlIsListedSemVer1,
                report.BaseUrlIsListedSemVer2);
        }

        public async Task<bool> IsConsistentAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter, allowPartial: true);
            return report.IsConsistent;
        }

        public Task PopulateStateAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            return Task.CompletedTask;
        }

        private async Task<MutableReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter,
            bool allowPartial)
        {
            var baseUrls = await _serviceIndexCache.GetUrlsAsync(ServiceIndexTypes.V2Search);
            var maxTries = 3;
            var incrementalProgress = new IncrementalProgress(progressReporter, baseUrls.Count * 2);
            var baseUrlHasPackageSemVer1 = new Dictionary<string, bool>();
            var baseUrlHasPackageSemVer2 = new Dictionary<string, bool>();
            var baseUrlIsListedSemVer1 = new Dictionary<string, bool>();
            var baseUrlIsListedSemVer2 = new Dictionary<string, bool>();

            var report = new MutableReport
            {
                IsConsistent = true,
                BaseUrlHasPackageSemVer1 = baseUrlHasPackageSemVer1,
                BaseUrlHasPackageSemVer2 = baseUrlHasPackageSemVer2,
                BaseUrlIsListedSemVer1 = baseUrlIsListedSemVer1,
                BaseUrlIsListedSemVer2 = baseUrlIsListedSemVer2,
            };

            var shouldExistSemVer1 = !context.IsDeleted && !context.IsSemVer2;
            var shouldBeListedSemVer1 = shouldExistSemVer1 && context.IsListed;
            var shouldExistSemVer2 = !context.IsDeleted;
            var shouldBeListedSemVer2 = shouldExistSemVer2 && context.IsListed;

            for (var i = 0; i < baseUrls.Count; i++)
            {
                var baseUrl = baseUrls[i];
                var isLastBaseUrl = i == baseUrls.Count - 1;

                try
                {
                    var packageSemVer1 = await _searchClient.GetPackageOrNullAsync(
                        baseUrl,
                        context.Id,
                        context.Version,
                        semVer2: false,
                        maxTries: maxTries);
                    var hasPackageSemVer1 = packageSemVer1 != null;
                    var isListedSemVer1 = packageSemVer1?.Listed ?? false;
                    baseUrlHasPackageSemVer1[baseUrl] = hasPackageSemVer1;
                    baseUrlIsListedSemVer1[baseUrl] = isListedSemVer1;
                    report.IsConsistent &= hasPackageSemVer1 == shouldExistSemVer1 && shouldBeListedSemVer1 == isListedSemVer1;
                    await incrementalProgress.ReportProgressAsync($"Searched for the package on search {baseUrl}, SemVer 1.0.0.");

                    if (allowPartial && !report.IsConsistent)
                    {
                        return report;
                    }

                    var packageSemVer2 = await _searchClient.GetPackageOrNullAsync(
                        baseUrl,
                        context.Id,
                        context.Version,
                        semVer2: true,
                        maxTries: maxTries);
                    var hasPackageSemVer2 = packageSemVer2 != null;
                    var isListedSemVer2 = packageSemVer2?.Listed ?? false;
                    baseUrlHasPackageSemVer2[baseUrl] = hasPackageSemVer2;
                    baseUrlIsListedSemVer2[baseUrl] = isListedSemVer2;
                    report.IsConsistent &= hasPackageSemVer2 == shouldExistSemVer2 && shouldBeListedSemVer2 == isListedSemVer2;
                    await incrementalProgress.ReportProgressAsync($"Searched for the package on search {baseUrl}, SemVer 2.0.0.");

                    if (allowPartial && !report.IsConsistent)
                    {
                        return report;
                    }
                }
                catch (Exception ex)
                {
                    if (isLastBaseUrl && (!baseUrlHasPackageSemVer1.Any() || !baseUrlHasPackageSemVer2.Any()))
                    {
                        throw;
                    }

                    _logger.LogWarning(ex, "Failed to check the consistency of search base URL {BaseUrl}.", baseUrl);
                }
            }

            return report;
        }

        private class MutableReport
        {
            public bool IsConsistent { get; set; }
            public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer1 { get; set; }
            public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer2 { get; set; }
            public IReadOnlyDictionary<string, bool> BaseUrlIsListedSemVer1 { get; set; }
            public IReadOnlyDictionary<string, bool> BaseUrlIsListedSemVer2 { get; set; }
        }
    }
}
