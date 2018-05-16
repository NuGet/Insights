namespace Knapcode.ExplorePackages.Logic
{
    public class PackageBlobNameProvider
    {
        public string GetLatestNuspecPath(string id, string version)
        {
            var packageSpecificPath = GetPackageSpecificPath(id, version);
            var lowerId = id.ToLowerInvariant();

            return $"{packageSpecificPath}/{lowerId}.nuspec";
        }

        public string GetLatestMZipBlobName(string id, string version)
        {
            var packageSpecificPath = GetPackageSpecificPath(id, version);
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();

            return $"{packageSpecificPath}/{lowerId}.{lowerVersion}.mzip";
        }

        private string GetPackageSpecificPath(string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();

            return $"{lowerId}/{lowerVersion}";
        }
    }
}
