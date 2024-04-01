// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Azure;

namespace NuGet.Insights.Worker.LoadPackageVersion
{
    public class PackageVersionEntity : ILatestPackageLeaf
    {
        public PackageVersionEntity()
        {
        }

        public PackageVersionEntity(ICatalogLeafItem item)
        {
            PartitionKey = GetPartitionKey(item.PackageId);
            RowKey = GetRowKey(item.PackageVersion);
            Url = item.Url;
            LeafType = item.LeafType;
            CommitId = item.CommitId;
            CommitTimestamp = item.CommitTimestamp;
            PackageId = item.PackageId;
            PackageVersion = item.PackageVersion;
        }

        public PackageVersionEntity(
            ICatalogLeafItem item,
            PackageDetailsCatalogLeaf details) : this(item)
        {
            Created = details.Created;
            IsListed = details.IsListed();
            OriginalVersion = details.VerbatimVersion;
            SemVerType = details.GetSemVerType();
            Published = details.Published;
            LastEdited = HandleOutOfRange(details.LastEdited);
        }

        /// <summary>
        /// Some old leaves have default timestamp values which cannot be stored as timestamps in Table Storage.
        /// Example: lastEdited in https://api.nuget.org/v3/catalog0/data/2015.02.01.06.22.45/adam.jsgenerator.1.1.0.json
        /// </summary>
        private DateTimeOffset? HandleOutOfRange(DateTimeOffset? timestamp)
        {
            if (timestamp.HasValue && timestamp.Value < StorageUtility.MinTimestamp)
            {
                return null;
            }

            return timestamp;
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
        public DateTimeOffset? Published { get; set; }
        public DateTimeOffset? LastEdited { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }

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
