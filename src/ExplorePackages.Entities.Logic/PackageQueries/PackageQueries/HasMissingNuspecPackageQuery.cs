using System;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
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
        public bool NeedsNuspec => true;
        public bool NeedsMZip => false;
        public bool IsV2Query => false;
        public TimeSpan Delay => TimeSpan.Zero;

        public async Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (context.IsDeleted)
            {
                return false;
            }

            if (!context.Nuspec.Exists
                || context.Nuspec.Document == null)
            {
                await _nuspecDownloader.StoreNuspecAsync(
                    context.Id,
                    context.Version,
                    CancellationToken.None);

                throw new InvalidOperationException($"The .nuspec for {context.Id} {context.Version} could not be loaded.");
            }

            return false;
        }
    }
}
