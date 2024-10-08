// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public class TaskState : ITableEntityWithClientRequestId
    {
        public TaskState()
        {
        }

        public TaskState(string storageSuffix, string partitionKey, string rowKey)
        {
            StorageSuffix = storageSuffix;
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public TaskStateKey GetKey()
        {
            return new TaskStateKey(StorageSuffix, PartitionKey, RowKey);
        }

        public string StorageSuffix { get; set; }
        public string Parameters { get; set; }
        public string Message { get; set; }
        public DateTimeOffset? Started { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }
    }
}
