// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights.Worker
{
    public class CatalogIndexScanMessage
    {
        [JsonProperty("c")]
        public string CursorName { get; set; }

        [JsonProperty("i")]
        public string ScanId { get; set; }

        [JsonProperty("ac")]
        public int AttemptCount { get; set; }
    }
}
