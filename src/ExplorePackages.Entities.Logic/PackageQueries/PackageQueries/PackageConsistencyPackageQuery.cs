using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
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
            var packageConsistencyContext = new PackageConsistencyContext(
                context.Id,
                context.Version,
                context.IsDeleted,
                context.IsSemVer2,
                context.IsListed,
                context.HasIcon);

            return await _packageConsistencyQuery.IsMatchAsync(packageConsistencyContext, state);
        }
    }
}
