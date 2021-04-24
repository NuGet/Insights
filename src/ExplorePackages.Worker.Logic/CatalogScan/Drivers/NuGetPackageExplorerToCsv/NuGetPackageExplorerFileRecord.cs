using System;

namespace Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv
{
    public partial record NuGetPackageExplorerFile : PackageRecord, ICsvRecord
    {
        public NuGetPackageExplorerFile()
        {
        }

        public NuGetPackageExplorerFile(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public string Name { get; set; }
        public string Extension { get; set; }
        public string TargetFramework { get; set; }
        public string TargetFrameworkIdentifier { get; set; }
        public string TargetFrameworkVersion { get; set; }

        [KustoType("dynamic")]
        public string CompilerFlags { get; set; }

        public bool? HasCompilerFlags { get; set; }
        public bool? HasSourceLink { get; set; }
        public bool? HasDebugInfo { get; set; }
    }
}
