// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Azure;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessCatalogScan : ITableEntityWithClientRequestId
    {
        public static string GetPartitionKey(string runId)
        {
            return $"b-{runId}";
        }

        public TimedReprocessCatalogScan()
        {
        }

        public TimedReprocessCatalogScan(string runId, CatalogScanDriverType driverType, string scanId, string storageSuffix)
        {
            PartitionKey = GetPartitionKey(runId);
            RowKey = driverType.ToString();
            RunId = runId;
            ScanId = scanId;
            StorageSuffix = storageSuffix;
        }

        [IgnoreDataMember]
        public CatalogScanDriverType DriverType => Enum.Parse<CatalogScanDriverType>(RowKey);

        public string RunId { get; set; }
        public string ScanId { get; set; }
        public string StorageSuffix { get; set; }
        public bool Completed { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }
    }
}
