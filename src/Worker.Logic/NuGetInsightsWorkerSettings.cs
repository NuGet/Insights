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
    }
}
