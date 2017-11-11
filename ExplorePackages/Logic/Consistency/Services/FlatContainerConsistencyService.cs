using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;

namespace Knapcode.ExplorePackages.Logic
{
    public class FlatContainerConsistencyService : IConsistencyService<FlatContainerConsistencyReport>
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly PackagesContainerClient _packagesContainer;
        private readonly FlatContainerClient _flatContainer;

        public FlatContainerConsistencyService(
            ServiceIndexCache serviceIndexCache,
            PackagesContainerClient packagesContainer,
            FlatContainerClient flatContainer)
        {
            _serviceIndexCache = serviceIndexCache;
            _packagesContainer = packagesContainer;
            _flatContainer = flatContainer;
        }

        public async Task<FlatContainerConsistencyReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var report = await GetReportAsync(context, state, allowPartial: false);
            return new FlatContainerConsistencyReport(
                report.IsConsistent,
                report.PackageContentMetadata,
                report.HasPackageManifest.Value,
                report.IsInIndex.Value);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var partialReport = await GetReportAsync(context, state, allowPartial: true);
            return partialReport.IsConsistent;
        }

        public async Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (state.FlatContainer.PackageContentMetadata != null)
            {
                return;
            }

            var baseUrl = await GetBaseUrlAsync();

            var packageContentMetadata = await _flatContainer.GetPackageContentMetadataAsync(
                   baseUrl,
                   context.Package.Id,
                   context.Package.Version);

            state.FlatContainer.PackageContentMetadata = packageContentMetadata;
        }

        private async Task<PartialReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state, bool allowPartial)
        {
            var partialReport = new PartialReport { IsConsistent = true };
            var baseUrl = await GetBaseUrlAsync();

            var shouldExist = !context.Package.Deleted;

            await PopulateStateAsync(context, state);
            partialReport.PackageContentMetadata = state.FlatContainer.PackageContentMetadata;
            partialReport.IsConsistent &= shouldExist == partialReport.PackageContentMetadata.Exists;

            if (allowPartial && !partialReport.IsConsistent)
            {
                return partialReport;
            }

            var hasPackageManifest = await _flatContainer.HasPackageManifestAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            partialReport.HasPackageManifest = hasPackageManifest;
            partialReport.IsConsistent &= shouldExist == hasPackageManifest;

            if (allowPartial && !partialReport.IsConsistent)
            {
                return partialReport;
            }

            var isInIndex = await _flatContainer.HasPackageInIndexAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            partialReport.IsInIndex = isInIndex;
            partialReport.IsConsistent &= shouldExist == isInIndex;

            if (allowPartial && !partialReport.IsConsistent)
            {
                return partialReport;
            }

            return partialReport;
        }

        private async Task<string> GetBaseUrlAsync()
        {
            return await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
        }

        private class PartialReport
        {
            public bool IsConsistent { get; set; }
            public BlobMetadata PackageContentMetadata { get; set; }
            public bool? HasPackageManifest { get; set; }
            public bool? IsInIndex { get; set; }
        }
    }
}
