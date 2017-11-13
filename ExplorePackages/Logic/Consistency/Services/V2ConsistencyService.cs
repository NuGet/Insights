using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2ConsistencyService : IConsistencyService<V2ConsistencyReport>
    {
        private readonly V2Client _client;
        private readonly ExplorePackagesSettings _settings;

        public V2ConsistencyService(
            V2Client client,
            ExplorePackagesSettings settings)
        {
            _client = client;
            _settings = settings;
        }

        public async Task<V2ConsistencyReport> GetReportAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport)
        {
            var report = await GetReportAsync(context, state, progressReport, allowPartial: false);
            return new V2ConsistencyReport(
                report.IsConsistent,
                report.HasPackageSemVer1.Value,
                report.HasPackageSemVer2.Value);
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
            var incrementalProgress = new IncrementalProgress(progressReport, 2);
            var shouldExistSemVer1 = !context.Package.Deleted && !context.IsSemVer2;
            var shouldExistSemVer2 = !context.Package.Deleted;

            var report = new MutableReport { IsConsistent = true };

            var hasPackageSemVer1 = await _client.HasPackageAsync(
                _settings.V2BaseUrl,
                context.Package.Id,
                context.Package.Version,
                semVer2: false);
            report.HasPackageSemVer1 = hasPackageSemVer1;
            report.IsConsistent &= shouldExistSemVer1 == hasPackageSemVer1;
            await incrementalProgress.ReportProgressAsync("Checked for the package in V2, SemVer 1.0.0.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            var hasPackageSemVer2 = await _client.HasPackageAsync(
                _settings.V2BaseUrl,
                context.Package.Id,
                context.Package.Version,
                semVer2: true);
            report.HasPackageSemVer2 = hasPackageSemVer2;
            report.IsConsistent &= shouldExistSemVer2 == hasPackageSemVer2;
            await incrementalProgress.ReportProgressAsync("Checked for the package in V2, SemVer 2.0.0.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            return report;
        }

        private class MutableReport
        {
            public bool IsConsistent { get; set; }
            public bool? HasPackageSemVer1 { get; set; }
            public bool? HasPackageSemVer2 { get; set; }
        }
    }
}
