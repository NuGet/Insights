using System;
using NuGetPe;

namespace Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv
{
    public partial record NuGetPackageExplorerRecord : PackageRecord, ICsvRecord<NuGetPackageExplorerRecord>
    {
        public NuGetPackageExplorerRecord()
        {
        }

        public NuGetPackageExplorerRecord(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = NuGetPackageExplorerResultType.Deleted;
        }

        public NuGetPackageExplorerRecord(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = NuGetPackageExplorerResultType.Available;
        }

        public NuGetPackageExplorerResultType ResultType { get; set; }

        public long PackageSize { get; set; }
        public SymbolValidationResult SourceLinkResult { get; set; }
        public DeterministicResult DeterministicResult { get; set; }
        public HasCompilerFlagsResult CompilerFlagsResult { get; set; }
        public bool IsSignedByAuthor { get; set; }

        public string Name { get; set; }
        public string Extension { get; set; }
        public string TargetFramework { get; set; }
        public string TargetFrameworkIdentifier { get; set; }
        public string TargetFrameworkVersion { get; set; }
        public string CompilerFlags { get; set; }
        public bool? HasCompilerFlags { get; set; }
        public bool? HasSourceLink { get; set; }
        public bool? HasDebugInfo { get; set; }
    }
}
