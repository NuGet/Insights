using Knapcode.MiniZip;

namespace Knapcode.ExplorePackages
{
    public class PackageArchiveMetadata
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public long Size { get; set; }
        public ZipDirectory ZipDirectory { get; set; }
    }
}
