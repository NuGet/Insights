// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights.Worker
{
    public class CatalogPageScanMessage
    {
        [JsonProperty("s")]
        public string StorageSuffix { get; set; }

        [JsonProperty("p")]
        public string ScanId { get; set; }

        [JsonProperty("r")]
        public string PageId { get; set; }
    }
}
