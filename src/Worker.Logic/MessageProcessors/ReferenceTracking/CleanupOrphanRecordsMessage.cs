// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsMessage<T> : ITaskStateMessage
        where T : ICsvRecord
    {
        [JsonPropertyName("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonPropertyName("ac")]
        public int AttemptCount { get; set; }

        [JsonPropertyName("ci")]
        public string CleanupId { get; set; }

        [JsonPropertyName("ss")]
        public string StorageSuffix { get; set; }
    }
}
