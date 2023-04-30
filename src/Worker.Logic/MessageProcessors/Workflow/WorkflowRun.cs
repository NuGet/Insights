// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowRun : ITableEntity
    {
        public static readonly string DefaultPartitionKey = string.Empty;

        public WorkflowRun()
        {
        }

        public WorkflowRun(string runId)
        {
            PartitionKey = DefaultPartitionKey;
            RowKey = runId;
        }

        [IgnoreDataMember]
        public string RunId => RowKey;

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public WorkflowRunState State { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Completed { get; set; }
        public int AttemptCount { get; set; }
    }
}
