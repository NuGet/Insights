// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    [DebuggerDisplay("{Index}: {ScheduledFor} (last processed: {LastProcessed})")]
    public class TimedReprocessBucket : ITableEntityWithClientRequestId
    {
        public static readonly string DefaultPartitionKey = "z-buckets";

        public TimedReprocessBucket()
        {
        }

        public TimedReprocessBucket(int bucket)
        {
            PartitionKey = DefaultPartitionKey;
            RowKey = BucketedPackage.GetBucketString(bucket);
            Index = bucket;
        }

        public int Index { get; set; }
        public DateTimeOffset ScheduledFor { get; set; }
        public DateTimeOffset? LastProcessed { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }
    }
}
