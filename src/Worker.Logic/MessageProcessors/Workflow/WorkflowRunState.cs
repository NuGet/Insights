// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.Workflow
{
    public enum WorkflowRunState
    {
        Created,
        TimedReprocessWorking,
        CatalogScanWorking,
        CleanupOrphanRecordsWorking,
        AuxiliaryFilesWorking,
        KustoIngestionWorking,
        Finalizing,
        Aborted,
        Complete,
    }

    public static class WorkflowRunStateExtensions
    {
        public static bool IsTerminal(this WorkflowRunState state)
        {
            return state == WorkflowRunState.Complete || state == WorkflowRunState.Aborted;
        }
    }
}
