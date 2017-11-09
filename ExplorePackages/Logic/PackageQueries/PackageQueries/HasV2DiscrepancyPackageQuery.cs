using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasV2DiscrepancyPackageQuery : IPackageQuery
    {
        private const string BaseUrl = "https://www.nuget.org/api/v2";
        private readonly V2Client _client;

        public HasV2DiscrepancyPackageQuery(V2Client client)
        {
            _client = client;
        }

        public string Name => PackageQueryNames.HasV2DiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasV2DiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageQueryContext context)
        {
            var shouldExist = !context.Package.Deleted;

            var actuallyExists = await _client.HasPackageAsync(
                BaseUrl,
                context.Package.Id,
                context.Package.Version);

            return shouldExist != actuallyExists;
        }
    }
}
