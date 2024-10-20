// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker
{
    public record PackageRecord : IPackageRecord
    {
        public PackageRecord()
        {
        }

        public PackageRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
        {
            this.Initialize(scanId, scanTimestamp, leaf);
        }

        public PackageRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            this.Initialize(scanId, scanTimestamp, leaf);
        }

        [KustoIgnore]
        public Guid? ScanId { get; set; }

        [KustoIgnore]
        public DateTimeOffset? ScanTimestamp { get; set; }

        public string LowerId { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }

        [Required]
        public DateTimeOffset CatalogCommitTimestamp { get; set; }

        public DateTimeOffset? Created { get; set; }

        protected int CompareTo(PackageRecord other)
        {
            return PackageRecordExtensions.CompareTo(this, other);
        }
    }
}
