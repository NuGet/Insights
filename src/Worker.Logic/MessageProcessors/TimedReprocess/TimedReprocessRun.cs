// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Azure;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessRun : ITableEntityWithClientRequestId
    {
        public static readonly string DefaultPartitionKey = "a-runs";

        public TimedReprocessRun()
        {
        }

        public TimedReprocessRun(string runId, IReadOnlyList<int> buckets)
        {
            var bucketRanges = BucketRange.BucketsToRanges(buckets);
            if (string.IsNullOrEmpty(bucketRanges))
            {
                throw new ArgumentException("At least one bucket must be specified.", nameof(buckets));
            }

            PartitionKey = DefaultPartitionKey;
            RowKey = runId;
            Created = DateTimeOffset.UtcNow;
            State = TimedReprocessState.Created;
            BucketRanges = bucketRanges;
        }

        [IgnoreDataMember]
        public string RunId => RowKey;

        public DateTimeOffset Created { get; set; }
        public TimedReprocessState State { get; set; }
        public string BucketRanges { get; set; }
        public DateTimeOffset? Started { get; set; }
        public DateTimeOffset? Completed { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }
    }
}
