using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class IsMissingFromCatalogQuery : IPackageQuery
    {
        public string Name => PackageQueryNames.IsMissingFromCatalogQuery;
        public string CursorName => CursorNames.IsMissingFromCatalogQuery;
        public bool NeedsNuspec => false;
        public bool NeedsMZip => false;
        public bool IsV2Query => true;
        public TimeSpan Delay => V2ToDatabaseProcessor.FuzzFactor;

        public Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (context.Package.CatalogPackage == null)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}
