using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasFlatContainerDiscrepancyPackageQuery : IPackageQuery
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _client;

        public HasFlatContainerDiscrepancyPackageQuery(
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient client)
        {
            _serviceIndexCache = serviceIndexCache;
            _client = client;
        }

        public string Name => PackageQueryNames.HasFlatContainerDiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasFlatContainerDiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageQueryContext context)
        {
            var baseUrl = await _serviceIndexCache.GetUrlAsync("PackageBaseAddress/3.0.0");

            var shouldExist = !context.Package.Deleted;

            var actuallyHasPackageContent = await _client.HasPackageContentAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            if (shouldExist != actuallyHasPackageContent)
            {
                return true;
            }

            var actuallyHasPackageManifest = await _client.HasPackageManifestAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            if (shouldExist != actuallyHasPackageManifest)
            {
                return true;
            }

            var actuallyExistsInIndex = await _client.HasPackageInIndexAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            if (shouldExist != actuallyExistsInIndex)
            {
                return true;
            }

            return false;
        }
    }
}
