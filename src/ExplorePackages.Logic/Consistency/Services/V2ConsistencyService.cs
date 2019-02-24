using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2ConsistencyService : IConsistencyService<V2ConsistencyReport>
    {
        private readonly V2Client _client;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _options;

        public V2ConsistencyService(
            V2Client client,
            IOptionsSnapshot<ExplorePackagesSettings> options)
        {
            _client = client;
            _options = options;
        }

        public async Task<V2ConsistencyReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter, allowPartial: false);
            return new V2ConsistencyReport(
                report.IsConsistent,
                report.HasPackageSemVer1.Value,
                report.HasPackageSemVer2.Value,
                report.IsListedSemVer1.Value,
                report.IsListedSemVer2.Value);
        }

        public async Task<bool> IsConsistentAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter, allowPartial: true);
            return report.IsConsistent;
        }

        public Task PopulateStateAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            return Task.CompletedTask;
        }

        private async Task<MutableReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter,
            bool allowPartial)
        {
            var incrementalProgress = new IncrementalProgress(progressReporter, 2);
            var shouldExistSemVer1 = !context.IsDeleted && !context.IsSemVer2;
            var shouldBeListedSemVer1 = shouldExistSemVer1 && context.IsListed;
            var shouldExistSemVer2 = !context.IsDeleted;
            var shouldBeListedSemVer2 = shouldExistSemVer2 && context.IsListed;

            var report = new MutableReport { IsConsistent = true };

            var packageSemVer1 = await _client.GetPackageOrNullAsync(
                _options.Value.V2BaseUrl,
                context.Id,
                context.Version,
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
                _options.Value.V2BaseUrl,
                context.Id,
                context.Version,
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
