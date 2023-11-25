// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsParameters
    {
        [JsonPropertyName("s")]
        public CleanupOrphanRecordsState State { get; set; }

        [JsonPropertyName("pk")]
        public string LastPartitionKey { get; set; }

        [JsonPropertyName("rk")]
        public string LastRowKey { get; set; }
    }
}
