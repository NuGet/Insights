// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageAssetToCsv
{
    public partial record PackageAsset : PackageRecord, IAggregatedCsvRecord<PackageAsset>
    {
        public PackageAsset()
        {
        }

        public PackageAsset(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageAssetResultType.Deleted;
        }

        public PackageAsset(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageAssetResultType resultType)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = resultType;
        }

        [Required]
        public PackageAssetResultType ResultType { get; set; }

        public PatternSetType? PatternSet { get; set; }
        public string PropertyAnyValue { get; set; }
        public string PropertyCodeLanguage { get; set; }
        public string PropertyTargetFrameworkMoniker { get; set; }
        public string PropertyLocale { get; set; }
        public string PropertyManagedAssembly { get; set; }
        public string PropertyMSBuild { get; set; }
        public string PropertyRuntimeIdentifier { get; set; }
        public string PropertySatelliteAssembly { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public string RoundTripTargetFrameworkMoniker { get; set; }
        public string FrameworkName { get; set; }
        public string FrameworkVersion { get; set; }
        public string FrameworkProfile { get; set; }
        public string PlatformName { get; set; }
        public string PlatformVersion { get; set; }

        public static string GetCsvCompactMessageSchemaName() => "cc.pat";

        public static List<PackageAsset> Prune(List<PackageAsset> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageAsset other)
        {
            var c = base.CompareTo(other);
            if (c != 0)
            {
                return c;
            }

            c = Comparer<PatternSetType?>.Default.Compare(PatternSet, other.PatternSet);
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
