// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Azure;
using NuGet.Versioning;

namespace NuGet.Insights.Worker.LoadPackageVersion
{
    public class PackageVersionEntity : ILatestPackageLeaf
    {
        public PackageVersionEntity()
        {
        }

        public PackageVersionEntity(
            ICatalogLeafItem item,
            DateTimeOffset? created,
            bool? listed,
            string originalVersion,
            SemVerType? semVerType)
        {
            PartitionKey = GetPartitionKey(item.PackageId);
            RowKey = GetRowKey(item.PackageVersion);
            Url = item.Url;
            LeafType = item.Type;
            CommitId = item.CommitId;
            CommitTimestamp = item.CommitTimestamp;
            PackageId = item.PackageId;
            PackageVersion = item.PackageVersion;
            Created = created;
            IsListed = listed;
            OriginalVersion = originalVersion;
            SemVerType = semVerType;
        }


        [IgnoreDataMember]
        public string LowerId => PartitionKey;

        [IgnoreDataMember]
        public string LowerVersion => RowKey;

        public string Prefix { get; set; }
        public string Url { get; set; }
        public CatalogLeafType LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public DateTimeOffset? Created { get; set; }
        public bool? IsListed { get; set; }
        public string OriginalVersion { get; set; }
        public SemVerType? SemVerType { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public static string GetPartitionKey(string id)
        {
            return id.ToLowerInvariant();
        }

        public static string GetRowKey(string version)
        {
            return NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        }
    }
}
