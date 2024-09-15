// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings
    {
        public bool AutoStartTimedReprocess { get; set; } = false;
        public bool TimedReprocessIsEnabled { get; set; } = true;

        public string TimedReprocessTableName { get; set; } = "timedreprocess";

        public int OldTimedReprocessRunsToKeep { get; set; } = 49;

        /// <summary>
        /// This is the desired amount of time it will take to reprocess all packages. For content like legacy README or
        /// symbol packages that can be modified without any event in the catalog, this is the maximum staleness of that
        /// information stored in NuGet.Insights.
        /// </summary>
        public TimeSpan TimedReprocessWindow { get; set; } = TimeSpan.FromDays(14);

        /// <summary>
        /// This is the frequency that the timed preprocess service processes a set of package buckets. If you're using
        /// the workflow system, this configuration value is overridden by <see cref="WorkflowFrequency"/>.
        /// </summary>
        public TimeSpan TimedReprocessFrequency { get; set; } = TimeSpan.FromHours(6);

        /// <summary>
        /// This is the maximum number of buckets to reprocess in a single execution. This configuration exists so that if
        /// the reprocessing flow is for a long time or takes too long, the next attempt won't overload the system by
        /// reprocessing too many packages at once. This number should be larger than <see cref="TimedReprocessWindow"/>
        /// divided by <see cref="TimedReprocessFrequency"/> so that the reprocessor does not get behind.
        /// </summary>
        public int TimedReprocessMaxBuckets { get; set; } = 50;
    }
}
