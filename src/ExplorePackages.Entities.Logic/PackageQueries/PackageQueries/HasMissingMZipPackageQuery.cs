using System;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
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
        public bool NeedsNuspec => false;
        public bool NeedsMZip => true;
        public bool IsV2Query => false;
        public TimeSpan Delay => TimeSpan.Zero;

        public async Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (context.IsDeleted)
            {
                return false;
            }

            if (!context.MZip.Exists
                || context.MZip.ZipDirectory == null)
            {
                await _mzipStore.StoreMZipAsync(
                    context.Id,
                    context.Version,
                    CancellationToken.None);

                throw new InvalidOperationException($"The .mzip for {context.Id} {context.Version} could not be loaded.");
            }

            return false;
        }
    }
}
