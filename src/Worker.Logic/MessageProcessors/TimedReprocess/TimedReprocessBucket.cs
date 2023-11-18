// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Azure;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    [DebuggerDisplay("{Index}: {LastProcessed}")]
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

        /// <summary>
        /// Note that this value won't necessary move to the current time with the bucket is processed. Instead,
        /// it moved forward in increments of <see cref="NuGetInsightsWorkerSettings.TimedReprocessWindow"/>. If the
        /// timed reprocess flow hasn't been run in a long time, is falling behind, or if it has never run before, this
        /// property will be set to a time not close to the current timestamp. Think of this value more as the time
        /// it was scheduled to be processed rather than when it was actually processed.
        /// </summary>
        public DateTimeOffset LastProcessed { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }
    }
}
