// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker
{
    public class CatalogPageScan : ITableEntity
    {
        public CatalogPageScan()
        {
        }

        public CatalogPageScan(string storageSuffix, string scanId, string pageId)
        {
            StorageSuffix = storageSuffix;
            PartitionKey = scanId;
            RowKey = pageId;
            Created = DateTimeOffset.UtcNow;
        }

        [IgnoreDataMember]
        public string ScanId => PartitionKey;

        [IgnoreDataMember]
        public string PageId => RowKey;

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public CatalogPageScanState State { get; set; }
        public CatalogScanDriverType DriverType { get; set; }
        public string DriverParameters { get; set; }
        public DateTimeOffset Min { get; set; }
        public DateTimeOffset Max { get; set; }
        public string Url { get; set; }
        public int Rank { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
