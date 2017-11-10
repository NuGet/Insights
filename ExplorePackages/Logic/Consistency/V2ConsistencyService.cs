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
            var shouldExist = !context.Package.Deleted;

            var hasPackage = await _client.HasPackageAsync(
                BaseUrl,
                context.Package.Id,
                context.Package.Version);

            var isConsistent = shouldExist == hasPackage;

            return new V2ConsistencyReport(isConsistent, hasPackage);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context)
        {
            var report = await GetReportAsync(context);
            return report.IsConsistent;
        }
    }
}
