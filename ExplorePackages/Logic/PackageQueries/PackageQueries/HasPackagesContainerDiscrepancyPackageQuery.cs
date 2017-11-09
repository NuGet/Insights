using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasPackagesContainerDiscrepancyPackageQuery : IPackageQuery
    {
        private const string BaseUrl = "https://api.nuget.org/packages";
        private readonly PackagesContainerClient _client;

        public HasPackagesContainerDiscrepancyPackageQuery(PackagesContainerClient client)
        {
            _client = client;
        }

        public string Name => PackageQueryNames.HasPackagesContainerDiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasPackagesContainerDiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageQueryContext context)
        {
            var shouldExist = !context.Package.Deleted;

            var actuallyExists = await _client.HasPackageContentAsync(BaseUrl, context.Package.Id, context.Package.Version);

            return shouldExist != actuallyExists;
        }
    }
}
