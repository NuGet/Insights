using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class FlatContainerConsistencyService : IConsistencyService<FlatContainerConsistencyReport>
    {
        public const string Type = "PackageBaseAddress/3.0.0";
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

        public async Task<FlatContainerConsistencyReport> GetReportAsync(PackageQueryContext context)
        {
            var report = await GetReportAsync(context, allowPartial: false);
            return new FlatContainerConsistencyReport(
                report.IsConsistent,
                report.HasPackageContent.Value,
                report.HasPackageManifest.Value,
                report.IsInIndex.Value,
                report.PackagesContainerMd5,
                report.FlatContainerMd5);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context)
        {
            var partialReport = await GetReportAsync(context, allowPartial: true);
            return partialReport.IsConsistent;
        }

        private async Task<PartialReport> GetReportAsync(PackageQueryContext context, bool allowPartial)
        {
            var partialReport = new PartialReport { IsConsistent = true };
            var baseUrl = await _serviceIndexCache.GetUrlAsync(Type);

            var shouldExist = !context.Package.Deleted;
            
            var hasPackageContent = await _flatContainer.HasPackageContentAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            partialReport.HasPackageContent = hasPackageContent;
            partialReport.IsConsistent &= shouldExist == hasPackageContent;

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

            var packagesContainerMd5 = await _packagesContainer.GetPackageMd5HeaderAsync(
                NuGetOrgConstants.PackagesContainerBaseUrl,
                context.Package.Id,
                context.Package.Version);
            partialReport.PackagesContainerMd5 = packagesContainerMd5;
            partialReport.IsConsistent &= shouldExist == (packagesContainerMd5 != null);

            if (allowPartial && !partialReport.IsConsistent)
            {
                return partialReport;
            }

            var flatContainerMd5 = await _flatContainer.GetPackageMd5HeaderAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            partialReport.FlatContainerMd5 = packagesContainerMd5;
            partialReport.IsConsistent &= shouldExist == (flatContainerMd5 != null);

            if (allowPartial && !partialReport.IsConsistent)
            {
                return partialReport;
            }

            return partialReport;
        }

        private class PartialReport
        {
            public bool IsConsistent { get; set; }
            public bool? HasPackageContent { get; set; }
            public bool? HasPackageManifest { get; set; }
            public bool? IsInIndex { get; set; }
            public string PackagesContainerMd5 { get; set; }
            public string FlatContainerMd5 { get; set; }
        }
    }
}
