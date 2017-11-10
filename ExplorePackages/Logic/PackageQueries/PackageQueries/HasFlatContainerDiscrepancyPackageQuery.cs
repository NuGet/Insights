using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasFlatContainerDiscrepancyPackageQuery : IPackageQuery
    {
        private readonly FlatContainerConsistencyService _service;

        public HasFlatContainerDiscrepancyPackageQuery(FlatContainerConsistencyService service)
        {
            _service = service;
        }

        public string Name => PackageQueryNames.HasFlatContainerDiscrepancyPackageQuery;
        public string CursorName => CursorNames.HasFlatContainerDiscrepancyPackageQuery;

        public async Task<bool> IsMatchAsync(PackageQueryContext context)
        {
            var isConsistent = await _service.IsConsistentAsync(context);
            return !isConsistent;
        }
    }
}
