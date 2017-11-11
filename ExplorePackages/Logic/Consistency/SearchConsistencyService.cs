using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<SearchConsistencyReport> GetReportAsync(PackageQueryContext context)
        {
            var baseUrls = await _serviceIndexCache.GetUrlsAsync(V2SearchType);
            var baseUrlHasPackage = new Dictionary<string, bool>();

            var shouldExist = !context.Package.Deleted;

            foreach (var baseUrl in baseUrls)
            {
                var hasPackage = await _searchClient.HasPackageAsync(
                    baseUrl,
                    context.Package.Id,
                    context.Package.Version);

                baseUrlHasPackage[baseUrl] = hasPackage;
            }

            var isConsistent = baseUrlHasPackage
                .Values
                .All(x => x == shouldExist);

            return new SearchConsistencyReport(
                isConsistent,
                baseUrlHasPackage);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context)
        {
            var report = await GetReportAsync(context);
            return report.IsConsistent;
        }
    }
}
