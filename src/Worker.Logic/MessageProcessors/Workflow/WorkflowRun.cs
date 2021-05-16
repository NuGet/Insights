using System;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages.Worker.Workflow
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

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public DateTimeOffset? MaxCommitTimestamp { get; set; }
        public WorkflowRunState State { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Completed { get; set; }

        public string GetRunId()
        {
            return RowKey;
        }
    }
}
