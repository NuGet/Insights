using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2ConsistencyService : IConsistencyService<V2ConsistencyReport>
    {
        private const string BaseUrl = "https://www.nuget.org/api/v2";
        private readonly V2Client _client;

        public V2ConsistencyService(V2Client client)
        {
            _client = client;
        }

        public async Task<V2ConsistencyReport> GetReportAsync(PackageQueryContext context)
        {
            var report = await GetReportAsync(context, allowPartial: false);
            return new V2ConsistencyReport(
                report.IsConsistent,
                report.HasPackageSemVer1.Value,
                report.HasPackageSemVer2.Value);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context)
        {
            var report = await GetReportAsync(context, allowPartial: true);
            return report.IsConsistent;
        }

        private async Task<PartialReport> GetReportAsync(PackageQueryContext context, bool allowPartial)
        {
            var shouldExistSemVer1 = !context.Package.Deleted && !context.IsSemVer2;
            var shouldExistSemVer2 = !context.Package.Deleted;

            var report = new PartialReport { IsConsistent = true };

            var hasPackageSemVer1 = await _client.HasPackageAsync(
                BaseUrl,
                context.Package.Id,
                context.Package.Version,
                semVer2: false);
            report.HasPackageSemVer1 = hasPackageSemVer1;
            report.IsConsistent &= shouldExistSemVer1 == hasPackageSemVer1;
            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            var hasPackageSemVer2 = await _client.HasPackageAsync(
                BaseUrl,
                context.Package.Id,
                context.Package.Version,
                semVer2: true);
            report.HasPackageSemVer2 = hasPackageSemVer2;
            report.IsConsistent &= shouldExistSemVer2 == hasPackageSemVer2;
            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            return report;
        }

        private class PartialReport
        {
            public bool IsConsistent { get; set; }
            public bool? HasPackageSemVer1 { get; set; }
            public bool? HasPackageSemVer2 { get; set; }
        }
    }
}
