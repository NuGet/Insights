// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsParameters
    {
        [JsonProperty("s")]
        public CleanupOrphanRecordsState State { get; set; }

        [JsonProperty("pk")]
        public string LastPartitionKey { get; set; }

        [JsonProperty("rk")]
        public string LastRowKey { get; set; }
    }
}
