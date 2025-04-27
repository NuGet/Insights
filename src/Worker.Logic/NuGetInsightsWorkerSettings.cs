// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings : NuGetInsightsSettings
    {
        public int TableScanTakeCount { get; set; } = StorageUtility.MaxTakeCount;

        public bool MoveTempToHome { get; set; } = false;

        public string SingletonTaskStateTableName { get; set; } = "singletontaskstates";

        public string TaskStateTableNamePrefix { get; set; } = "taskstate";

        public bool EnableDiagnosticTracingToLogger { get; set; } = false;

        /// <summary>
        /// This should be larger than the Azure Function message timeout. If the requeue happens too early then it
        /// might trigger too much and cause unnecessary message duplication.
        /// </summary>
        public TimeSpan FanOutRequeueTime { get; set; } = TimeSpan.FromMinutes(20);

        /// <summary>
        /// The message queues must have less than this number of messages for a requeue operation to occurr. If the
        /// requeue happens too early might trigger when the message queue just has a lot of messages and the fan out
        /// work items haven't been processed because the work is on the queue but it hasn't been reached yet in the queue.
        /// </summary>
        public int FanOutRequeueMaxMessageCount { get; set; } = 10;

        /// <summary>
        /// Patterns for package IDs to exclude from the catalog scan.
        /// </summary>
        public List<IgnoredPackagePattern> IgnoredPackages { get; set; } = [];

        public class CopyWorkerStorageSettings : IConfigureOptions<NuGetInsightsWorkerSettings>
        {
            public void Configure(NuGetInsightsWorkerSettings options)
            {
            }
        }
    }
}
