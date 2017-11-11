using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public abstract class HasRegistrationDiscrepancyPackageQuery : IPackageQuery
    {
        private readonly RegistrationConsistencyService _service;

        public HasRegistrationDiscrepancyPackageQuery(
            RegistrationConsistencyService service,
            string name,
            string cursorName)
        {
            _service = service;
            Name = name;
            CursorName = cursorName;
        }

        public string Name { get; }
        public string CursorName { get; }

        public async Task<bool> IsMatchAsync(PackageQueryContext context)
        {
            var isConsistent = await _service.IsConsistentAsync(context, new PackageConsistencyState());
            return !isConsistent;
        }
    }
}
