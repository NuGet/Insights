// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Azure;
using Azure.Data.Tables;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    [DebuggerDisplay("{Index}: {LastProcessed}")]
    public class Bucket : ITableEntity
    {
        public static readonly string DefaultPartitionKey = "buckets";

        public Bucket()
        {
        }

        public Bucket(int bucket)
        {
            PartitionKey = DefaultPartitionKey;
            RowKey = BucketedPackage.GetBucketString(bucket);
            Index = bucket;
        }

        public int Index { get; set; }
        public DateTimeOffset LastProcessed { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
