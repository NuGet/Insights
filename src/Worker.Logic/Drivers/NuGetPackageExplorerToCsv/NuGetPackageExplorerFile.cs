// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using NuGetPe.AssemblyMetadata;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public partial record NuGetPackageExplorerFile : PackageRecord, IAggregatedCsvRecord<NuGetPackageExplorerFile>
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

        [Required]
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

        public static string GetCsvCompactMessageSchemaName() => "cc.npef";

        public static List<NuGetPackageExplorerFile> Prune(List<NuGetPackageExplorerFile> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(NuGetPackageExplorerFile other)
        {
            var c = base.CompareTo(other);
            if (c != 0)
            {
                return c;
            }

            return string.CompareOrdinal(Path, other.Path);
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}
