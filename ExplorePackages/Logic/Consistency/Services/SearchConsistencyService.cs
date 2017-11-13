using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchConsistencyService : IConsistencyService<SearchConsistencyReport>
    {
        private readonly SearchServiceUrlDiscoverer _discoverer;
        private readonly SearchClient _searchClient;
        private readonly bool _specificInstances;

        public SearchConsistencyService(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient searchClient,
            bool specificInstances)
        {
            _discoverer = discoverer;
            _searchClient = searchClient;
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
                report.BaseUrlHasPackageSemVer2);
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
            var incrementalProgress = new IncrementalProgress(progressReport, baseUrls.Count * 2);
            var baseUrlHasPackageSemVer1 = new Dictionary<string, bool>();
            var baseUrlHasPackageSemVer2 = new Dictionary<string, bool>();

            var report = new MutableReport
            {
                IsConsistent = true,
                BaseUrlHasPackageSemVer1 = baseUrlHasPackageSemVer1,
                BaseUrlHasPackageSemVer2 = baseUrlHasPackageSemVer2,
            };

            var shouldExistSemVer1 = !context.Package.Deleted && !context.IsSemVer2;
            var shouldExistSemVer2 = !context.Package.Deleted;

            foreach (var baseUrl in baseUrls)
            {
                var hasPackageSemVer1 = await _searchClient.HasPackageAsync(
                    baseUrl,
                    context.Package.Id,
                    context.Package.Version,
                    semVer2: false);
                baseUrlHasPackageSemVer1[baseUrl] = hasPackageSemVer1;
                report.IsConsistent &= hasPackageSemVer1 == shouldExistSemVer1;
                await incrementalProgress.ReportProgressAsync($"Searched for the package on search {baseUrl}, SemVer 1.0.0.");

                if (allowPartial && !report.IsConsistent)
                {
                    return report;
                }

                var hasPackageSemVer2 = await _searchClient.HasPackageAsync(
                    baseUrl,
                    context.Package.Id,
                    context.Package.Version,
                    semVer2: true);
                baseUrlHasPackageSemVer2[baseUrl] = hasPackageSemVer2;
                report.IsConsistent &= hasPackageSemVer2 == shouldExistSemVer2;
                await incrementalProgress.ReportProgressAsync($"Searched for the package on search {baseUrl}, SemVer 2.0.0.");

                if (allowPartial && !report.IsConsistent)
                {
                    return report;
                }
            }

            return report;
        }

        private class MutableReport
        {
            public bool IsConsistent { get; set; }
            public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer1 { get; set; }
            public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer2 { get; set; }
        }
    }
}
