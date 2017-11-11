using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasSearchDiscrepancyPackageQuery : IPackageQuery
    {
        private readonly SearchLoadBalancerConsistencyService _service;

        public HasSearchDiscrepancyPackageQuery(SearchLoadBalancerConsistencyService service)
        {
            _service = service;
        }

        public string Name => PackageQueryNames.HasSearchDiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasSearchDiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var isConsistent = await _service.IsConsistentAsync(context, state);
            return !isConsistent;
        }
    }
}
