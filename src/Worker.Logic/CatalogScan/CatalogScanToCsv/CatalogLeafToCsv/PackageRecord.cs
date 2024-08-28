// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public record PackageRecord
    {
        public PackageRecord()
        {
        }

        public PackageRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : this(scanId, scanTimestamp, leaf.PackageId, leaf.PackageVersion, leaf.CommitTimestamp, created: null)
        {
        }

        public PackageRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : this(scanId, scanTimestamp, leaf.PackageId, leaf.PackageVersion, leaf.CommitTimestamp, leaf.Created)
        {
        }

        public PackageRecord(Guid scanId, DateTimeOffset scanTimestamp, string id, string version, DateTimeOffset catalogCommitTimestamp, DateTimeOffset? created)
        {
            ScanId = scanId;
            ScanTimestamp = scanTimestamp;
            Id = id;
            var parsedVersion = NuGetVersion.Parse(version);
            Version = parsedVersion.ToNormalizedString();
            LowerId = id.ToLowerInvariant();
            Identity = GetIdentity(LowerId, Version);
            CatalogCommitTimestamp = catalogCommitTimestamp;
            Created = created;
        }

        [KustoIgnore]
        public Guid? ScanId { get; set; }

        [KustoIgnore]
        public DateTimeOffset? ScanTimestamp { get; set; }

        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public DateTimeOffset CatalogCommitTimestamp { get; set; }
        public DateTimeOffset? Created { get; set; }

        public static List<T> Prune<T>(List<T> records, bool isFinalPrune) where T : PackageRecord, IEquatable<T>, IComparable<T>
        {
            var pruned = records
                .GroupBy(x => x, PackageRecordIdVersionComparer.Instance) // Group by unique package version
                .Select(g => g
                    .GroupBy(x => new { x.ScanId, x.CatalogCommitTimestamp }) // Group package version records by scan and catalog commit timestamp
                    .OrderByDescending(x => x.Key.CatalogCommitTimestamp)
                    .ThenByDescending(x => x.First().ScanTimestamp ?? DateTimeOffset.MinValue)
                    .First())
                .SelectMany(g => g)
                .Distinct()
                .Order()
                .ToList();

            if (isFinalPrune)
            {
                foreach (var record in pruned)
                {
                    record.ScanId = null;
                    record.ScanTimestamp = null;
                }
            }

            return pruned;
        }

        public int CompareTo(PackageRecord other)
        {
            return CompareTo(LowerId, Identity, other.LowerId, other.Identity);
        }

        public static int CompareTo(string lowerIdA, string identityA, string lowerIdB, string identityB)
        {
            var c = string.CompareOrdinal(lowerIdA, lowerIdB);
            if (c != 0)
            {
                return c;
            }

            var startIndex = lowerIdA.Length + 1;
            var length = (identityA.Length - startIndex) + 1;
            return string.CompareOrdinal(identityA, startIndex, identityB, startIndex, length);
        }

        public static string GetIdentity(string lowerId, string normalizedVersion)
        {
#if DEBUG
            if (lowerId != lowerId.ToLowerInvariant())
            {
                throw new ArgumentException("The lower ID must be lowercase.", nameof(lowerId));
            }

            if (normalizedVersion != NuGetVersion.Parse(normalizedVersion).ToNormalizedString())
            {
                throw new ArgumentException("The version must be normalized.", nameof(normalizedVersion));
            }
#endif

            return $"{lowerId}/{normalizedVersion.ToLowerInvariant()}";
        }
    }
}
