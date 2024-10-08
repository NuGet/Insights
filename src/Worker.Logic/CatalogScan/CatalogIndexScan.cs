// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Azure;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public class CatalogIndexScan : ITableEntityWithClientRequestId
    {
        public CatalogIndexScan()
        {
        }

        public CatalogIndexScan(CatalogScanDriverType driverType, string scanId, string storageSuffix)
        {
            PartitionKey = driverType.ToString();
            RowKey = scanId;
            StorageSuffix = storageSuffix;
            Created = DateTimeOffset.UtcNow;
        }

        [IgnoreDataMember]
        public CatalogScanDriverType DriverType => CatalogScanDriverType.Parse(PartitionKey);

        [IgnoreDataMember]
        public string ScanId => RowKey;

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public CatalogIndexScanState State { get; set; }
        public bool OnlyLatestLeaves { get; set; }

        [IgnoreDataMember]
        public CatalogScanDriverType? ParentDriverType { get; set; }

        [DataMember(Name = nameof(ParentDriverType))]
        public string ParentDriverTypeName
        {
            get => ParentDriverType?.ToString();
            set => ParentDriverType = value is null ? null : CatalogScanDriverType.Parse(value);
        }

        public string ParentScanId { get; set; }
        public string CursorName { get; set; }
        public DateTimeOffset Min { get; set; }
        public DateTimeOffset Max { get; set; }
        public string BucketRanges { get; set; }
        public DateTimeOffset? Started { get; set; }
        public CatalogIndexScanResult? Result { get; set; }
        public DateTimeOffset? Completed { get; set; }
        public bool ContinueUpdate { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }
    }
}
