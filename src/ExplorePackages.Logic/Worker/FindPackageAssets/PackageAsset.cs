using System;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class PackageAsset
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public DateTimeOffset Created { get; set; }
        public string PatternSet { get; set; }
        public string Framework { get; set; }
        public string Path { get; set; }
    }
}
