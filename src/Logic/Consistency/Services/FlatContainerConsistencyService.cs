using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class FlatContainerConsistencyService : IConsistencyService<FlatContainerConsistencyReport>
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainer;

        public FlatContainerConsistencyService(
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainer)
        {
            _serviceIndexCache = serviceIndexCache;
            _flatContainer = flatContainer;
        }

        public async Task<FlatContainerConsistencyReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter, allowPartial: false);
            return new FlatContainerConsistencyReport(
                report.IsConsistent,
                report.PackageContentMetadata,
                report.HasPackageManifest.Value,
                report.HasPackageIcon.Value,
                report.IsInIndex.Value);
        }

        public async Task<bool> IsConsistentAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter, allowPartial: true);
            return report.IsConsistent;
        }

        public async Task PopulateStateAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            if (state.FlatContainer.PackageContentMetadata != null)
            {
                return;
            }

            var baseUrl = await GetBaseUrlAsync();

            var packageContentMetadata = await _flatContainer.GetPackageContentMetadataAsync(
                   baseUrl,
                   context.Id,
                   context.Version);

            state.FlatContainer.PackageContentMetadata = packageContentMetadata;
        }

        private async Task<MutableReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter,
            bool allowPartial)
        {
            var report = new MutableReport { IsConsistent = true };
            var incrementalProgress = new IncrementalProgress(progressReporter, 3);
            var baseUrl = await GetBaseUrlAsync();

            var shouldExist = !context.IsDeleted;
            var shouldHaveIcon = shouldExist && context.HasIcon;

            await PopulateStateAsync(context, state, progressReporter);
            report.PackageContentMetadata = state.FlatContainer.PackageContentMetadata;
            report.IsConsistent &= shouldExist == report.PackageContentMetadata.Exists;
            await incrementalProgress.ReportProgressAsync("Check for the package content in flat container.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            var hasPackageManifest = await _flatContainer.HasPackageManifestAsync(
                baseUrl,
                context.Id,
                context.Version);
            report.HasPackageManifest = hasPackageManifest;
            report.IsConsistent &= shouldExist == hasPackageManifest;
            await incrementalProgress.ReportProgressAsync("Checked for the package manifest in flat container.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            var hasPackageIcon = await _flatContainer.HasPackageIconAsync(
                baseUrl,
                context.Id,
                context.Version);
            report.HasPackageIcon = hasPackageIcon;
            report.IsConsistent &= shouldHaveIcon == hasPackageIcon;
            await incrementalProgress.ReportProgressAsync("Checked for the package icon in flat container.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            var isInIndex = await _flatContainer.HasPackageInIndexAsync(
                baseUrl,
                context.Id,
                context.Version);
            report.IsInIndex = isInIndex;
            report.IsConsistent &= shouldExist == isInIndex;
            await incrementalProgress.ReportProgressAsync("Checked for the package in flat container index.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            return report;
        }

        private async Task<string> GetBaseUrlAsync()
        {
            return await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
        }

        private class MutableReport
        {
            public bool IsConsistent { get; set; }
            public BlobMetadata PackageContentMetadata { get; set; }
            public bool? HasPackageManifest { get; set; }
            public bool? HasPackageIcon { get; set; }
            public bool? IsInIndex { get; set; }
        }
    }
}
