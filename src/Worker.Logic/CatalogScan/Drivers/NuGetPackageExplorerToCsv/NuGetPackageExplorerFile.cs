// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetPe.AssemblyMetadata;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public partial record NuGetPackageExplorerFile : PackageRecord, ICsvRecord
    {
        public NuGetPackageExplorerFile()
        {
        }

        public NuGetPackageExplorerFile(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = NuGetPackageExplorerResultType.Deleted;
        }

        public NuGetPackageExplorerFile(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = NuGetPackageExplorerResultType.Available;
        }

        public NuGetPackageExplorerResultType ResultType { get; set; }

        public string Path { get; set; }
        public string Extension { get; set; }

        public bool? HasCompilerFlags { get; set; }
        public bool? HasSourceLink { get; set; }
        public bool? HasDebugInfo { get; set; }

        [KustoType("dynamic")]
        public string CompilerFlags { get; set; }

        [KustoType("dynamic")]
        public string SourceUrlRepoInfo { get; set; }

        public PdbType? PdbType { get; set; }
    }
}
