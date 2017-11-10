using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackagesContainerConsistencyService : IConsistencyService<PackagesContainerConsistencyReport>
    {
        private const string BaseUrl = "https://api.nuget.org/packages";
        private readonly PackagesContainerClient _client;

        public PackagesContainerConsistencyService(PackagesContainerClient client)
        {
            _client = client;
        }

        public async Task<PackagesContainerConsistencyReport> GetReportAsync(PackageQueryContext context)
        {
            var shouldExist = !context.Package.Deleted;

            var hasPackageContent = await _client.HasPackageContentAsync(
                BaseUrl,
                context.Package.Id,
                context.Package.Version);

            var isConsistent = shouldExist == hasPackageContent;

            return new PackagesContainerConsistencyReport(
                isConsistent,
                hasPackageContent);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context)
        {
            var report = await GetReportAsync(context);
            return report.IsConsistent;
        }
    }
}
