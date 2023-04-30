// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker
{
    public class CatalogIndexScan : ITableEntity
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
        public CatalogScanDriverType DriverType => Enum.Parse<CatalogScanDriverType>(PartitionKey);

        [IgnoreDataMember]
        public string ScanId => RowKey;

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public CatalogIndexScanState State { get; set; }
        public string DriverParameters { get; set; }
        public string CursorName { get; set; }
        public DateTimeOffset? Min { get; set; }
        public DateTimeOffset? Max { get; set; }
        public DateTimeOffset? Started { get; set; }
        public CatalogIndexScanResult? Result { get; set; }
        public DateTimeOffset? Completed { get; set; }
        public bool ContinueUpdate { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
