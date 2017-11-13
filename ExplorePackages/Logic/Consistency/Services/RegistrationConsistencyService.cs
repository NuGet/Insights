using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationConsistencyService : IConsistencyService<RegistrationConsistencyReport>
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly RegistrationClient _client;
        private readonly string _type;
        private readonly bool _hasSemVer2;

        public RegistrationConsistencyService(
            ServiceIndexCache serviceIndexCache,
            RegistrationClient client,
            string type,
            bool hasSemVer2)
        {
            _serviceIndexCache = serviceIndexCache;
            _client = client;
            _type = type;
            _hasSemVer2 = hasSemVer2;
        }

        public async Task<RegistrationConsistencyReport> GetReportAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport)
        {
            var report = await GetReportAsync(context, state, progressReport, 
                allowPartial: false);
            return new RegistrationConsistencyReport(
                report.IsConsistent,
                report.IsInIndex.Value,
                report.HasLeaf.Value);
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
            var report = new MutableReport { IsConsistent = true };
            var incrementalProgress = new IncrementalProgress(progressReport, 2);
            var baseUrl = await _serviceIndexCache.GetUrlAsync(_type);

            var shouldExist = !context.Package.Deleted && (_hasSemVer2 || !context.IsSemVer2);

            var isInIndex = await _client.HasPackageInIndexAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            report.IsInIndex = isInIndex;
            report.IsConsistent &= shouldExist == isInIndex;
            await incrementalProgress.ReportProgressAsync("Checked for the package in the registration index.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            var hasLeaf = await _client.HasLeafAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            report.HasLeaf = hasLeaf;
            report.IsConsistent &= shouldExist == hasLeaf;
            await incrementalProgress.ReportProgressAsync("Checked for the package's registration leaf.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            return report;
        }

        private class MutableReport
        {
            public bool IsConsistent { get; set; }
            public bool? IsInIndex { get; set; }
            public bool? HasLeaf { get; set; }
        }
    }
}
