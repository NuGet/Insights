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
                report.HasPackageSemVer2.Value,
                report.IsListedSemVer1.Value,
                report.IsListedSemVer2.Value);
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
            var shouldBeListedSemVer1 = shouldExistSemVer1 && context.IsListed;
            var shouldExistSemVer2 = !context.Package.Deleted;
            var shouldBeListedSemVer2 = shouldExistSemVer2 && context.IsListed;

            var report = new MutableReport { IsConsistent = true };

            var packageSemVer1 = await _client.GetPackageOrNullAsync(
                _settings.V2BaseUrl,
                context.Package.Id,
                context.Package.Version,
                semVer2: false);
            report.HasPackageSemVer1 = packageSemVer1 != null;
            report.IsListedSemVer1 = packageSemVer1?.Listed ?? false;
            report.IsConsistent &= shouldExistSemVer1 == report.HasPackageSemVer1 && shouldBeListedSemVer1 == report.IsListedSemVer1;
            await incrementalProgress.ReportProgressAsync("Checked for the package in V2, SemVer 1.0.0.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            var packageSemVer2 = await _client.GetPackageOrNullAsync(
                _settings.V2BaseUrl,
                context.Package.Id,
                context.Package.Version,
                semVer2: true);
            report.HasPackageSemVer2 = packageSemVer2 != null;
            report.IsListedSemVer2 = packageSemVer2?.Listed ?? false;
            report.IsConsistent &= shouldExistSemVer2 == report.HasPackageSemVer2 && shouldBeListedSemVer2 == report.IsListedSemVer2;
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
            public bool? IsListedSemVer1 { get; set; }
            public bool? IsListedSemVer2 { get; set; }
        }
    }
}
