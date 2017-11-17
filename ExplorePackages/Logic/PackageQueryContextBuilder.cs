using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Entities;
using NuGet.Common;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContextBuilder
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly GalleryConsistencyService _galleryConsistencyService;
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger _log;

        public PackageQueryContextBuilder(
            PackagePathProvider pathProvider,
            GalleryConsistencyService galleryConsistencyService,
            ExplorePackagesSettings settings,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _galleryConsistencyService = galleryConsistencyService;
            _settings = settings;
            _log = log;
        }

        public PackageQueryContext CreateDeletedPackageQueryContext(string id, string version)
        {
            return CreatePackageQueryContext(id, version, isSemVer2: false, deleted: true);
        }

        public PackageQueryContext CreateAvailablePackageQueryContext(string id, string version, bool isSemVer2)
        {
            return CreatePackageQueryContext(id, version, isSemVer2, deleted: false);
        }

        private PackageQueryContext CreatePackageQueryContext(string id, string version, bool isSemVer2, bool deleted)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersion = parsedVersion.ToNormalizedString();
            var fullVersion = parsedVersion.ToFullString();
            var immutablePackage = new ImmutablePackage(new Package
            {
                Key = 0,
                Id = id,
                Version = normalizedVersion,
                Identity = $"{id}/{normalizedVersion}",
                Deleted = deleted,
                FirstCommitTimestamp = 0,
                LastCommitTimestamp = 0,
            });

            var nuspecQueryContext = new NuspecQueryContext(
                path: null,
                exists: false,
                document: null);

            return new PackageQueryContext(immutablePackage, nuspecQueryContext, isSemVer2, fullVersion);
        }
        public async Task<PackageQueryContext> GetPackageQueryContextFromGalleryAsync(string id, string version, PackageConsistencyState state)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            var initialContext = CreateAvailablePackageQueryContext(id, version, isSemVer2: false);

            await _galleryConsistencyService.PopulateStateAsync(initialContext, state, NullProgressReport.Instance);
            
            return CreatePackageQueryContext(
                state.Gallery.PackageState.PackageId,
                state.Gallery.PackageState.PackageVersion,
                state.Gallery.PackageState.IsSemVer2,
                state.Gallery.PackageState.PackageDeletedStatus != PackageDeletedStatus.NotDeleted);
        }

        public async Task<PackageQueryContext> GetPackageQueryContextFromDatabaseAsync(string id, string version)
        {
            var packageService = new PackageService(_log);
            var package = await packageService.GetPackageAsync(id, version);
            if (package == null)
            {
                return null;
            }

            return GetPackageQueryFromDatabasePackageContext(package);
        }

        public PackageQueryContext GetPackageQueryFromDatabasePackageContext(Package package)
        {
            var immutablePackage = new ImmutablePackage(package);
            var nuspecQueryContext = GetNuspecQueryContext(package);
            var isSemVer2 = NuspecUtility.IsSemVer2(nuspecQueryContext.Document);

            var originalVersion = NuspecUtility.GetOriginalVersion(nuspecQueryContext.Document);
            string fullVersion = null;
            if (NuGetVersion.TryParse(originalVersion, out var parsedVersion))
            {
                fullVersion = parsedVersion.ToFullString();
            }

            return new PackageQueryContext(immutablePackage, nuspecQueryContext, isSemVer2, fullVersion);
        }

        private NuspecQueryContext GetNuspecQueryContext(Package package)
        {
            var path = _pathProvider.GetLatestNuspecPath(package.Id, package.Version);
            var exists = false;
            XDocument document = null;
            try
            {
                if (File.Exists(path))
                {
                    exists = true;
                    using (var stream = File.OpenRead(path))
                    {
                        document = NuspecUtility.LoadXml(stream);
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogError($"Could not parse .nuspec for {package.Id} {package.Version}: {path}"
                    + Environment.NewLine
                    + "  "
                    + e.Message);

                throw;
            }

            if (!exists && !package.Deleted)
            {
                _log.LogWarning($"Could not find .nuspec for {package.Id} {package.Version}: {path}");
            }

            return new NuspecQueryContext(path, exists, document);
        }

    }
}
