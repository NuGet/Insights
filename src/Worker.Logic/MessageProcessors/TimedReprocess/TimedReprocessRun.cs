// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessRun : ITableEntity
    {
        public static readonly string DefaultPartitionKey = "runs";

        public TimedReprocessRun()
        {
        }

        public TimedReprocessRun(string runId)
        {
            PartitionKey = DefaultPartitionKey;
            RowKey = runId;
            Created = DateTimeOffset.UtcNow;
            State = TimedReprocessState.Created;
        }

        public DateTimeOffset Created { get; set; }
        public TimedReprocessState State { get; set; }
        public string Buckets { get; set; }
        public DateTimeOffset? Started { get; set; }
        public DateTimeOffset? Completed { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string GetRunId()
        {
            return RowKey;
        }
    }
}
