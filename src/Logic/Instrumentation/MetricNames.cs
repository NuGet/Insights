// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    /// <summary>
    /// These are metric names that should not change due to some integration (e.g. auto-scaling, alerting).
    /// </summary>
    public static class MetricNames
    {
        public const string MessageProcessedCount = "MessageProcessedCount";
        public const string BatchMessageProcessorDurationMs = "BatchMessageProcessorDurationMs";
        public const string MessageProcessorDurationMs = "MessageProcessorDurationMs";

        public const string CatalogScanUpdate = "CatalogScan.Update";

        public const string WorkflowStateTransition = "Workflow.StateTransition";

        public const string TimerExecute = "Timer.Execute";

        public const string SinceLastWorkflowCompletedHours = "SinceLastWorkflowCompletedHours";

        public const string StorageQueueSize = "StorageQueueSize";
        public const string StorageQueueSizeMain = "StorageQueueSize.Main";
        public const string StorageQueueSizePoison = "StorageQueueSize.Poison";
        public const string StorageQueueSizeWorkMain = "StorageQueueSize.Work.Main";
        public const string StorageQueueSizeWorkPoison = "StorageQueueSize.Work.Poison";
        public const string StorageQueueSizeExpandMain = "StorageQueueSize.Expand.Main";
        public const string StorageQueueSizeExpandPoison = "StorageQueueSize.Expand.Poison";

        public const string CsvBlobCount = "CsvBlob.Count";
        public const string CsvBlobRecordCount = "CsvBlob.RecordCount";
        public const string CsvBlobCompressedSize = "CsvBlob.CompressedSize";
        public const string CsvBlobUncompressedSize = "CsvBlob.UncompressedSize";
    }
}
