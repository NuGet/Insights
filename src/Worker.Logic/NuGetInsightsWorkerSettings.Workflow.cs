// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings
    {
        public string WorkflowRunTableName { get; set; } = "workflowruns";

        public int OldWorkflowRunsToKeep { get; set; } = 49;

        public int WorkflowMaxAttempts { get; set; } = 5;

        public TimeSpan WorkflowFrequency { get; set; } = TimeSpan.FromDays(1);
    }
}
