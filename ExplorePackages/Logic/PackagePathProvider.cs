using System.IO;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackagePathProvider
    {
        private readonly string _baseDir;

        public PackagePathProvider(string baseDir)
        {
            _baseDir = baseDir;
        }

        public string GetPackageSpecificPath(string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();

            return Path.Combine(_baseDir, lowerId, lowerVersion);
        }

        public string GetUniqueNuspecPath(string id, string version, string hash)
        {
            var packageSpecificPath = GetPackageSpecificPath(id, version);
            var lowerHash = hash.ToLowerInvariant();

            return Path.Combine(packageSpecificPath, "nuspecs", $"{lowerHash}.nuspec");
        }

        public string GetLatestNuspecPath(string id, string version)
        {
            var packageSpecificPath = GetPackageSpecificPath(id, version);
            var lowerId = id.ToLowerInvariant();

            return Path.Combine(packageSpecificPath, $"{lowerId}.nuspec");
        }
    }
}
