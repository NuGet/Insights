using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public class PackageConsistencyPackageQuery : IPackageQuery
    {
        private readonly IPackageConsistencyQuery _packageConsistencyQuery;

        public PackageConsistencyPackageQuery(IPackageConsistencyQuery packageConsistencyQuery)
        {
            _packageConsistencyQuery = packageConsistencyQuery;
        }

        public string Name => _packageConsistencyQuery.Name;
        public string CursorName => _packageConsistencyQuery.CursorName;
        public bool NeedsNuspec => false;
        public bool NeedsMZip => false;
        public bool IsV2Query => false;
        public TimeSpan Delay => TimeSpan.Zero;

        public async Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var consistencyContext = context.Package.ToConsistencyContext();
            return await _packageConsistencyQuery.IsMatchAsync(consistencyContext, state);
        }
    }
}
