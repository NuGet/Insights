using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchConsistencyService : IConsistencyService<SearchConsistencyReport>
    {
        private const string V2SearchType = "SearchGalleryQueryService/3.0.0-rc";
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly SearchClient _searchClient;

        public SearchConsistencyService(
            ServiceIndexCache serviceIndexCache,
            SearchClient searchClient)
        {
            _serviceIndexCache = serviceIndexCache;
            _searchClient = searchClient;
        }

        public async Task<SearchConsistencyReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var report = await GetReportAsync(context, state, allowPartial: false);
            return new SearchConsistencyReport(
                report.IsConsistent,
                report.BaseUrlHasPackageSemVer1,
                report.BaseUrlHasPackageSemVer2);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var report = await GetReportAsync(context, state, allowPartial: true);
            return report.IsConsistent;
        }

        public Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            return Task.CompletedTask;
        }

        private async Task<PartialReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state, bool allowPartial)
        {
            var baseUrls = await _serviceIndexCache.GetUrlsAsync(V2SearchType);
            var baseUrlHasPackageSemVer1 = new Dictionary<string, bool>();
            var baseUrlHasPackageSemVer2 = new Dictionary<string, bool>();

            var report = new PartialReport
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
                if (allowPartial && !report.IsConsistent)
                {
                    return report;
                }
            }

            return report;
        }

        private class PartialReport
        {
            public bool IsConsistent { get; set; }
            public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer1 { get; set; }
            public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer2 { get; set; }
        }
    }
}
