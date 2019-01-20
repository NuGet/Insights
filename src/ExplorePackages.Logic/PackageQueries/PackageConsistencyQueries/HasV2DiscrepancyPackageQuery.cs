using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasV2DiscrepancyPackageQuery : IPackageConsistencyQuery
    {
        private readonly V2ConsistencyService _service;

        public HasV2DiscrepancyPackageQuery(V2ConsistencyService service)
        {
            _service = service;
        }

        public string Name => PackageQueryNames.HasV2DiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasV2DiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageConsistencyContext context, PackageConsistencyState state)
        {
            var isConsistent = await _service.IsConsistentAsync(context, state, NullProgressReporter.Instance);
            return !isConsistent;
        }
    }
}
