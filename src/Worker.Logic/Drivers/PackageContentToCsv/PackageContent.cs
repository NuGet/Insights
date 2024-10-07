// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageContentToCsv
{
    public partial record PackageContent : PackageRecord, IAggregatedCsvRecord<PackageContent>, IPackageEntryRecord
    {
        public PackageContent()
        {
        }

        public PackageContent(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageContentResultType.Deleted;
        }

        public PackageContent(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageContentResultType resultType)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = resultType;
        }

        [Required]
        public PackageContentResultType ResultType { get; set; }

        public string Path { get; set; }
        public string FileExtension { get; set; }
        public int? SequenceNumber { get; set; }
        public int? Size { get; set; }
        public bool? Truncated { get; set; }
        public int? TruncatedSize { get; set; }
        public string SHA256 { get; set; }
        public string Content { get; set; }
        public bool? DuplicateContent { get; set; }

        public static string GetCsvCompactMessageSchemaName() => "cc.pcn";

        public static IEqualityComparer<PackageContent> GetKeyComparer() => IPackageEntryRecord.PackageEntryKeyComparer<PackageContent>.Instance;

        public static List<PackageContent> Prune(List<PackageContent> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageContent other)
        {
            var c = base.CompareTo(other);
            if (c != 0)
            {
                return c;
            }

            return Comparer<int?>.Default.Compare(SequenceNumber, other.SequenceNumber);
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}
