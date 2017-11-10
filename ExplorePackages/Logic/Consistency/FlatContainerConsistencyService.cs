using System.Threading.Tasks;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class FlatContainerConsistencyService : IConsistencyService<FlatContainerConsistencyReport>
    {
        public const string Type = "PackageBaseAddress/3.0.0";
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _client;

        public FlatContainerConsistencyService(ServiceIndexCache serviceIndexCache, FlatContainerClient client)
        {
            _serviceIndexCache = serviceIndexCache;
            _client = client;
        }

        public async Task<FlatContainerConsistencyReport> GetReportAsync(PackageQueryContext context)
        {
            var report = await GetReportAsync(context, allowPartial: false);
            return new FlatContainerConsistencyReport(
                report.IsConsistent,
                report.HasPackageContent.Value,
                report.HasPackageManifest.Value,
                report.IsInIndex.Value);
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
            
            var hasPackageContent = await _client.HasPackageContentAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            partialReport.HasPackageContent = hasPackageContent;
            partialReport.IsConsistent &= shouldExist == hasPackageContent;

            if (allowPartial && !partialReport.IsConsistent)
            {
                return partialReport;
            }

            var hasPackageManifest = await _client.HasPackageManifestAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            partialReport.HasPackageManifest = hasPackageManifest;
            partialReport.IsConsistent &= shouldExist == hasPackageManifest;

            if (allowPartial && !partialReport.IsConsistent)
            {
                return partialReport;
            }

            var isInIndex = await _client.HasPackageInIndexAsync(
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

        private class PartialReport
        {
            public bool IsConsistent { get; set; }
            public bool? HasPackageContent { get; set; }
            public bool? HasPackageManifest { get; set; }
            public bool? IsInIndex { get; set; }
        }
    }
}
