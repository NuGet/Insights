// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Website
{
    public class AdminViewModel
    {
        public QueueViewModel WorkQueue { get; set; }
        public QueueViewModel ExpandQueue { get; set; }

        public bool IsWorkflowRunning { get; set; }

        public DateTimeOffset DefaultMax { get; set; }
        public IReadOnlyList<CatalogScanViewModel> CatalogScans { get; set; }
        public IReadOnlyList<WorkflowRun> WorkflowRuns { get; set; }
        public TimedReprocessViewModel TimedReprocess { get; set; }
        public IReadOnlyList<KustoIngestionEntity> KustoIngestions { get; set; }
        public IReadOnlyList<TimerState> TimerStates { get; set; }
    }
}
