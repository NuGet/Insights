using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasPackagesContainerDiscrepancyPackageQuery : IPackageQuery
    {
        private readonly PackagesContainerConsistencyService _service;

        public HasPackagesContainerDiscrepancyPackageQuery(PackagesContainerConsistencyService service)
        {
            _service = service;
        }

        public string Name => PackageQueryNames.HasPackagesContainerDiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasPackagesContainerDiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var isConsistent = await _service.IsConsistentAsync(context, state, NullProgressReport.Instance);
            return !isConsistent;
        }
    }
}
