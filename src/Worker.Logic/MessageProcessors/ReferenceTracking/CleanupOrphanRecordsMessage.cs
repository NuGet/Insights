// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsMessage<T> : ITaskStateMessage
        where T : ICsvRecord
    {
        [JsonProperty("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonProperty("ac")]
        public int AttemptCount { get; set; }

        [JsonProperty("ci")]
        public string CleanupId { get; set; }

        [JsonProperty("ss")]
        public string StorageSuffix { get; set; }
    }
}
