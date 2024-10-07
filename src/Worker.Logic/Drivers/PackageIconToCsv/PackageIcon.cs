// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    public partial record PackageIcon : PackageRecord, IAggregatedCsvRecord<PackageIcon>
    {
        public PackageIcon()
        {
        }

        public PackageIcon(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageIconResultType.Deleted;
        }

        public PackageIcon(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageIconResultType.Available;
        }

        [Required]
        public PackageIconResultType ResultType { get; set; }

        public long? FileLength { get; set; }
        public string FileSHA256 { get; set; }
        public string ContentType { get; set; }
        public string HeaderFormat { get; set; }

        public bool? AutoDetectedFormat { get; set; }
        public string Signature { get; set; }
        public long? Width { get; set; }
        public long? Height { get; set; }
        public int? FrameCount { get; set; }
        public bool? IsOpaque { get; set; }

        [KustoType("dynamic")]
        public string FrameFormats { get; set; }

        [KustoType("dynamic")]
        public string FrameDimensions { get; set; }

        [KustoType("dynamic")]
        public string FrameAttributeNames { get; set; }

        public static string GetCsvCompactMessageSchemaName() => "cc.pi";

        public static IEqualityComparer<PackageIcon> GetKeyComparer() => IdentityComparer<PackageIcon>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = IdentityKeyField;

        public static List<PackageIcon> Prune(List<PackageIcon> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageIcon other)
        {
            return base.CompareTo(other);
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}
