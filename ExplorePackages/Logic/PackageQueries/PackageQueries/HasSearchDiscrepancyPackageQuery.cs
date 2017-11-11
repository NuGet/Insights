using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasSearchDiscrepancyPackageQuery : IPackageQuery
    {
        private readonly SearchConsistencyService _service;

        public HasSearchDiscrepancyPackageQuery(SearchConsistencyService service)
        {
            _service = service;
        }

        public string Name => PackageQueryNames.HasSearchDiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasSearchDiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageQueryContext context)
        {
            var isConsistent = await _service.IsConsistentAsync(context);
            return !isConsistent;
        }
    }
}
