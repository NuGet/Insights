// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings : NuGetInsightsSettings
    {
        public bool UseBulkEnqueueStrategy { get; set; } = true;
        public int BulkEnqueueThreshold { get; set; } = 10;
        public int EnqueueWorkers { get; set; } = 1;
        public int MaxBulkEnqueueMessageCount { get; set; } = 50;
        public bool AllowBatching { get; set; } = true;
        public bool DisableMessageDelay { get; set; } = false;

        public string WorkQueueName { get; set; } = "work";
        public string ExpandQueueName { get; set; } = "expand";
    }
}
