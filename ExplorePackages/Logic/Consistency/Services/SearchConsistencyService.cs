using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public abstract class SearchConsistencyService : IConsistencyService<SearchConsistencyReport>
    {
        private readonly SearchServiceUrlDiscoverer _discoverer;
        private readonly SearchClient _searchClient;
        private readonly ILogger _logger;
        private readonly bool _specificInstances;

        public SearchConsistencyService(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient searchClient,
            ILogger logger,
            bool specificInstances)
        {
            _discoverer = discoverer;
            _searchClient = searchClient;
            _logger = logger;
            _specificInstances = specificInstances;
        }

        public async Task<SearchConsistencyReport> GetReportAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport)
        {
            var report = await GetReportAsync(context, state, progressReport, allowPartial: false);
            return new SearchConsistencyReport(
                report.IsConsistent,
                report.BaseUrlHasPackageSemVer1,
                report.BaseUrlHasPackageSemVer2,
                report.BaseUrlIsListedSemVer1,
                report.BaseUrlIsListedSemVer2);
        }

        public async Task<bool> IsConsistentAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport)
        {
            var report = await GetReportAsync(context, state, progressReport, allowPartial: true);
            return report.IsConsistent;
        }

        public Task PopulateStateAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport)
        {
            return Task.CompletedTask;
        }

        private async Task<MutableReport> GetReportAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport,
            bool allowPartial)
        {
            var baseUrls = await _discoverer.GetUrlsAsync(ServiceIndexTypes.V2Search, _specificInstances);
            var maxTries = _specificInstances ? 1 : 3;
            var incrementalProgress = new IncrementalProgress(progressReport, baseUrls.Count * 2);
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

            var shouldExistSemVer1 = !context.Package.Deleted && !context.IsSemVer2;
            var shouldBeListedSemVer1 = shouldExistSemVer1 && context.IsListed;
            var shouldExistSemVer2 = !context.Package.Deleted;
            var shouldBeListedSemVer2 = shouldExistSemVer2 && context.IsListed;

            for (var i = 0; i < baseUrls.Count; i++)
            {
                var baseUrl = baseUrls[i];
                var isLastBaseUrl = i == baseUrls.Count - 1;

                try
                {
                    var packageSemVer1 = await _searchClient.GetPackageOrNullAsync(
                        baseUrl,
                        context.Package.Id,
                        context.Package.Version,
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
                        context.Package.Id,
                        context.Package.Version,
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
