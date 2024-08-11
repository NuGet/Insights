// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetPe;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public partial record NuGetPackageExplorerRecord : PackageRecord, ICsvRecord
    {
        public NuGetPackageExplorerRecord()
        {
        }

        public NuGetPackageExplorerRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = NuGetPackageExplorerResultType.Deleted;
        }

        public NuGetPackageExplorerRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = NuGetPackageExplorerResultType.Available;
        }

        public NuGetPackageExplorerResultType ResultType { get; set; }

        public SymbolValidationResult? SourceLinkResult { get; set; }
        public DeterministicResult? DeterministicResult { get; set; }
        public HasCompilerFlagsResult? CompilerFlagsResult { get; set; }
        public bool? IsSignedByAuthor { get; set; }
    }
}
