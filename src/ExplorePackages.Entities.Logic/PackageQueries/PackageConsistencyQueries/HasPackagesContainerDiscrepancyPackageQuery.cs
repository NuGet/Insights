using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public class HasPackagesContainerDiscrepancyPackageQuery : IPackageConsistencyQuery
    {
        private readonly PackagesContainerConsistencyService _service;

        public HasPackagesContainerDiscrepancyPackageQuery(PackagesContainerConsistencyService service)
        {
            _service = service;
        }

        public string Name => PackageQueryNames.HasPackagesContainerDiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasPackagesContainerDiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageConsistencyContext context, PackageConsistencyState state)
        {
            var isConsistent = await _service.IsConsistentAsync(context, state, NullProgressReporter.Instance);
            return !isConsistent;
        }
    }
}
