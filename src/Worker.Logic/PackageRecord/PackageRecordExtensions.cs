// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    public static class PackageRecordExtensions
    {
        public static IReadOnlyList<string> IdentityKeyField { get; } = [nameof(IPackageRecord.Identity)];

        public static IReadOnlyList<string> PackageEntryKeyFields { get; } = [nameof(IPackageRecord.Identity), nameof(IPackageEntryRecord.SequenceNumber)];

        public static int CompareTo<T>(T record, T other) where T : IPackageRecord
        {
            return CompareTo(record.LowerId, record.Identity, other.LowerId, other.Identity);
        }

        /// <summary>
        /// Instead of just comparing the identity, this method compares the lower ID and the identity separately so that
        /// records are sorted first by ID and then by version (both done case-insensitive). This groups records first by
        /// ID and then by version instead of having sort complications at the '/' identity separator.
        /// </summary>
        public static int CompareTo(string lowerIdA, string identityA, string lowerIdB, string identityB)
        {
#if DEBUG
            if (identityA.Length < lowerIdA.Length + "/1.0.0".Length)
            {
                throw new ArgumentException("The first identity must be be longer than the first lower ID, a '/', and a version", nameof(identityA));
            }

            if (!identityA.StartsWith(lowerIdA, StringComparison.Ordinal))
            {
                throw new ArgumentException("The first identity must start with the first lower ID.", nameof(lowerIdA));
            }

            if (identityA[lowerIdA.Length] != '/')
            {
                throw new ArgumentException("The first identity must have a '/' after the lower ID.", nameof(identityA));
            }

            if (identityB.Length < lowerIdB.Length + "/1.0.0".Length)
            {
                throw new ArgumentException("The second identity must be be longer than the second lower ID, a '/', and a version", nameof(identityA));
            }

            if (!identityB.StartsWith(lowerIdB, StringComparison.Ordinal))
            {
                throw new ArgumentException("The second identity must start with the second lower ID.", nameof(lowerIdA));
            }

            if (identityB[lowerIdB.Length] != '/')
            {
                throw new ArgumentException("The second identity must have a '/' after the lower ID.", nameof(identityA));
            }
#endif

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

        public static void Initialize<T>(this T record, Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) where T : IPackageRecord
        {
            record.Initialize(scanId, scanTimestamp, leaf.PackageId, leaf.PackageVersion, leaf.CommitTimestamp, created: null);
        }

        public static void Initialize<T>(this T record, Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) where T : IPackageRecord
        {
            record.Initialize(scanId, scanTimestamp, leaf.PackageId, leaf.PackageVersion, leaf.CommitTimestamp, leaf.Created);
        }

        public static void Initialize<T>(this T record, Guid scanId, DateTimeOffset scanTimestamp, string id, string version, DateTimeOffset catalogCommitTimestamp, DateTimeOffset? created) where T : IPackageRecord
        {
            record.ScanId = scanId;
            record.ScanTimestamp = scanTimestamp;
            record.Id = id;
            var parsedVersion = NuGetVersion.Parse(version);
            record.Version = parsedVersion.ToNormalizedString();
            record.LowerId = id.ToLowerInvariant();
            record.Identity = GetIdentity(record.LowerId, record.Version);
            record.CatalogCommitTimestamp = catalogCommitTimestamp;
            record.Created = created;
        }

        public static List<T> Prune<T>(List<T> records, bool isFinalPrune) where T : IPackageRecord, IEquatable<T>, IComparable<T>
        {
            var pruned = records
                .GroupBy(x => x, PackageRecordIdVersionComparer<T>.Instance) // Group by unique package version
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
    }
}
