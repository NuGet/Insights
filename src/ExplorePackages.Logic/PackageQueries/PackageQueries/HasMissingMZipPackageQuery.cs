using System;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class HasMissingMZipPackageQuery : IPackageQuery
    {
        private readonly MZipStore _mzipStore;

        public HasMissingMZipPackageQuery(MZipStore mzipStore)
        {
            _mzipStore = mzipStore;
        }

        public string Name => PackageQueryNames.HasMissingMZipPackageQuery;
        public string CursorName => CursorNames.HasMissingMZipPackageQuery;

        public async Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (context.Package.Deleted)
            {
                return false;
            }

            if (!context.MZip.Exists
                || context.MZip.ZipDirectory == null)
            {
                await _mzipStore.StoreMZipAsync(
                    context.Package.Id,
                    context.Package.Version,
                    CancellationToken.None);

                throw new InvalidOperationException($"The .mzip for {context.Package.Id} {context.Package.Version} could not be loaded.");
            }

            return false;
        }
    }
}
