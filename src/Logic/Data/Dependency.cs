using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    public class Dependency
    {
        public Dependency(string id, string version, VersionRange parsedVersionRange)
        {
            Id = id;
            Version = version;
            ParsedVersionRange = parsedVersionRange;
        }

        public string Id { get; }
        public string Version { get; }
        public VersionRange ParsedVersionRange { get; }
    }
}
