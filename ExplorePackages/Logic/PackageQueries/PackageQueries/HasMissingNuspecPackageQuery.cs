using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasMissingNuspecPackageQuery : IPackageQuery
    {
        public string Name => PackageQueryNames.FindMissingNuspecPackageQuery;
        public string CursorName => CursorNames.FindMissingNuspecPackageQuery;
        
        public Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (context.Package.Deleted)
            {
                return Task.FromResult(false);
            }

            if (!context.Nuspec.Exists
                || context.Nuspec.Document == null)
            {
                throw new InvalidOperationException($"The .nuspec for {context.Package.Id} {context.Package.Version} could not be loaded.");
            }

            return Task.FromResult(false);
        }
    }
}
