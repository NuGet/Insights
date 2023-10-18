// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

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
            Identity = GetIdentity(LowerId, parsedVersion);
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

        public static List<T> Prune<T>(List<T> records, bool isFinalPrune) where T : PackageRecord, IEquatable<T>
        {
            var pruned = records
                .GroupBy(x => x, PackageRecordIdVersionComparer.Instance) // Group by unique package version
                .Select(g => g
                    .GroupBy(x => new { x.ScanId, x.CatalogCommitTimestamp }) // Group package version records by scan and catalog commit timestamp
                    .OrderByDescending(x => x.Key.CatalogCommitTimestamp)
                    .ThenByDescending(x => x.First().ScanTimestamp ?? DateTimeOffset.MinValue)
                    .First())
                .SelectMany(g => g)
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .Distinct()
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

        public static string GetBucketKey(ICatalogLeafItem item)
        {
            return GetIdentity(item);
        }

        public static string GetIdentity(ICatalogLeafItem item)
        {
            return GetIdentity(item.PackageId, item.ParsePackageVersion());
        }

        public static string GetIdentity(string id, NuGetVersion version)
        {
            return $"{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}";
        }
    }
}
