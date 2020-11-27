using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public class HasSearchDiscrepancyPackageQuery : IPackageConsistencyQuery
    {
        private readonly SearchConsistencyService _service;

        public HasSearchDiscrepancyPackageQuery(SearchConsistencyService service)
        {
            _service = service;
        }

        public string Name => PackageQueryNames.HasSearchDiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasSearchDiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageConsistencyContext context, PackageConsistencyState state)
        {
            var isConsistent = await _service.IsConsistentAsync(context, state, NullProgressReporter.Instance);
            return !isConsistent;
        }
    }
}
