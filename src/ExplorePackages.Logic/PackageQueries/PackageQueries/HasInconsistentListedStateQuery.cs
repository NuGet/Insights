using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasInconsistentListedStateQuery : IPackageQuery
    {
        public string Name => PackageQueryNames.HasInconsistentListedStateQuery;
        public string CursorName => CursorNames.HasInconsistentListedStateQuery;
        public bool NeedsNuspec => false;
        public bool NeedsMZip => false;
        public bool IsV2Query => false;
        public TimeSpan Delay => TimeSpan.Zero;

        public Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (context.Package.V2Package?.Listed != context.Package.CatalogPackage?.Listed)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}
