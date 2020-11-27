using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContextBuilder
    {
        private readonly NuspecStore _nuspecStore;
        private readonly MZipStore _mzipStore;
        private readonly ILogger<PackageQueryContextBuilder> _logger;

        public PackageQueryContextBuilder(
            NuspecStore nuspecStore,
            MZipStore mzipStore,
            ILogger<PackageQueryContextBuilder> logger)
        {
            _nuspecStore = nuspecStore;
            _mzipStore = mzipStore;
            _logger = logger;
        }

        public async Task<PackageQueryContext> GetPackageQueryContextFromDatabaseAsync(
            PackageEntity package,
            bool includeNuspec,
            bool includeMZip)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            NuspecContext nuspecContext = null;
            if (includeNuspec)
            {
                nuspecContext = await GetNuspecContextAsync(package);
            }

            MZipContext mzipContext = null;
            if (includeMZip)
            {
                mzipContext = await GetMZipContextAsync(package);
            }

            return new PackageQueryContext(
                package,
                nuspecContext,
                mzipContext);
        }

        private async Task<MZipContext> GetMZipContextAsync(PackageEntity package)
        {
            var context = await _mzipStore.GetMZipContextAsync(package.PackageRegistration.Id, package.Version);

            if (!context.Exists && !package.CatalogPackage.Deleted)
            {
                _logger.LogWarning(
                    "Could not find .mzip for {Id} {Version}.",
                    package.PackageRegistration.Id,
                    package.Version);
            }

            return context;
        }

        private async Task<NuspecContext> GetNuspecContextAsync(PackageEntity package)
        {
            var context = await _nuspecStore.GetNuspecContextAsync(package.PackageRegistration.Id, package.Version);

            if (!context.Exists && !package.CatalogPackage.Deleted)
            {
                _logger.LogWarning(
                    "Could not find .nuspec for {Id} {Version}.",
                    package.PackageRegistration.Id,
                    package.Version);
            }

            return context;
        }
    }
}
