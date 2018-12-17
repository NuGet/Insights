using System;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasMissingNuspecPackageQuery : IPackageQuery
    {
        private readonly NuspecStore _nuspecDownloader;

        public HasMissingNuspecPackageQuery(NuspecStore nuspecDownloader)
        {
            _nuspecDownloader = nuspecDownloader;
        }

        public string Name => PackageQueryNames.HasMissingNuspecPackageQuery;
        public string CursorName => CursorNames.HasMissingNuspecPackageQuery;
        
        public async Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (context.Package.Deleted)
            {
                return false;
            }

            if (!context.Nuspec.Exists
                || context.Nuspec.Document == null)
            {
                await _nuspecDownloader.StoreNuspecAsync(
                    context.Package.Id,
                    context.Package.Version,
                    CancellationToken.None);

                throw new InvalidOperationException($"The .nuspec for {context.Package.Id} {context.Package.Version} could not be loaded.");
            }

            return false;
        }
    }
}
