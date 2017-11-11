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

        public async Task<RegistrationConsistencyReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var report = await GetReportAsync(context, state, allowPartial: false);
            return new RegistrationConsistencyReport(
                report.IsConsistent,
                report.IsInIndex.Value,
                report.HasLeaf.Value);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var report = await GetReportAsync(context, state, allowPartial: true);
            return report.IsConsistent;
        }

        public Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            return Task.CompletedTask;
        }

        private async Task<PartialReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state, bool allowPartial)
        {
            var partialReport = new PartialReport { IsConsistent = true };
            var baseUrl = await _serviceIndexCache.GetUrlAsync(_type);

            var shouldExist = !context.Package.Deleted && (_hasSemVer2 || !context.IsSemVer2);

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

            var hasLeaf = await _client.HasLeafAsync(
                baseUrl,
                context.Package.Id,
                context.Package.Version);
            partialReport.HasLeaf = hasLeaf;
            partialReport.IsConsistent &= shouldExist == hasLeaf;

            if (allowPartial && !partialReport.IsConsistent)
            {
                return partialReport;
            }

            return partialReport;
        }

        private class PartialReport
        {
            public bool IsConsistent { get; set; }
            public bool? IsInIndex { get; set; }
            public bool? HasLeaf { get; set; }
        }
    }
}
