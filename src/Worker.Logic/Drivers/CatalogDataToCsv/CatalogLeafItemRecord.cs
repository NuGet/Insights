// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using NuGet.Packaging;

namespace NuGet.Insights.Worker.CatalogDataToCsv
{
    public partial record CatalogLeafItemRecord : IAggregatedCsvRecord<CatalogLeafItemRecord>
    {
        public CatalogLeafItemRecord()
        {
        }

        public CatalogLeafItemRecord(PackageDetailsCatalogLeaf leaf, string pageUrl) : this((CatalogLeaf)leaf, pageUrl)
        {
            IsListed = leaf.IsListed();
            Created = leaf.Created;
            LastEdited = leaf.LastEdited;
            Published = leaf.Published;
            PackageSize = leaf.PackageSize;
            PackageHash = leaf.PackageHash;
            PackageHashAlgorithm = leaf.PackageHashAlgorithm;
            Deprecation = KustoDynamicSerializer.Serialize(leaf.Deprecation);
            Vulnerabilities = KustoDynamicSerializer.Serialize(leaf.Vulnerabilities);
            HasRepositoryProperty = leaf.Repository is not null;

            if (leaf.PackageEntries is not null)
            {
                PackageEntryCount = leaf.PackageEntries.Count;
                NuspecPackageEntry = KustoDynamicSerializer.Serialize(leaf
                    .PackageEntries
                    .FirstOrDefault(e => PackageHelper.IsNuspec(e.FullName)));
                SignaturePackageEntry = KustoDynamicSerializer.Serialize(leaf
                    .PackageEntries
                    .FirstOrDefault(e => e.FullName == ".signature.p7s"));
            }
        }

        public CatalogLeafItemRecord(PackageDeleteCatalogLeaf leaf, string pageUrl) : this((CatalogLeaf)leaf, pageUrl)
        {
            Published = leaf.Published;
        }

        private CatalogLeafItemRecord(CatalogLeaf leaf, string pageUrl)
        {
            CommitId = leaf.CommitId;
            CommitTimestamp = leaf.CommitTimestamp;
            LowerId = leaf.PackageId.ToLowerInvariant();
            Identity = $"{LowerId}/{leaf.ParsePackageVersion().ToNormalizedString().ToLowerInvariant()}";
            Id = leaf.PackageId;
            Version = leaf.PackageVersion;
            Type = leaf.LeafType;
            Url = leaf.Url;
            PageUrl = pageUrl ?? throw new ArgumentNullException(nameof(pageUrl));
        }

        public string CommitId { get; set; }

        [Required]
        public DateTimeOffset CommitTimestamp { get; set; }

        public string LowerId { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }

        [Required]
        public CatalogLeafType Type { get; set; }

        public string Url { get; set; }

        public string PageUrl { get; set; }

        public DateTimeOffset? Published { get; set; }

        public bool? IsListed { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? LastEdited { get; set; }
        public long? PackageSize { get; set; }
        public string PackageHash { get; set; }
        public string PackageHashAlgorithm { get; set; }

        [KustoType("dynamic")]
        public string Deprecation { get; set; }

        [KustoType("dynamic")]
        public string Vulnerabilities { get; set; }

        public bool? HasRepositoryProperty { get; set; }
        public int? PackageEntryCount { get; set; }

        [KustoType("dynamic")]
        public string NuspecPackageEntry { get; set; }

        [KustoType("dynamic")]
        public string SignaturePackageEntry { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.cl";
        public static IEqualityComparer<CatalogLeafItemRecord> KeyComparer { get; } = CatalogLeafItemRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(Identity), nameof(CommitTimestamp)];

        public static List<CatalogLeafItemRecord> Prune(List<CatalogLeafItemRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return records
                .Distinct()
                .Order()
                .ToList();
        }

        public int CompareTo(CatalogLeafItemRecord other)
        {
            var c = PackageRecord.CompareTo(LowerId, Identity, other.LowerId, other.Identity);
            if (c != 0)
            {
                return c;
            }

            return CommitTimestamp.CompareTo(other.CommitTimestamp);
        }

        public class CatalogLeafItemRecordKeyComparer : IEqualityComparer<CatalogLeafItemRecord>
        {
            public static CatalogLeafItemRecordKeyComparer Instance { get; } = new CatalogLeafItemRecordKeyComparer();

            public bool Equals(CatalogLeafItemRecord x, CatalogLeafItemRecord y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Identity == y.Identity
                    && x.CommitTimestamp == y.CommitTimestamp;
            }

            public int GetHashCode([DisallowNull] CatalogLeafItemRecord obj)
            {
                var hashCode = new HashCode();
                hashCode.Add(obj.Identity);
                hashCode.Add(obj.CommitTimestamp);
                return hashCode.ToHashCode();
            }
        }
    }
}
