// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogPageScanMessage
    {
        [JsonPropertyName("s")]
        public string StorageSuffix { get; set; }

        [JsonPropertyName("p")]
        public string ScanId { get; set; }

        [JsonPropertyName("r")]
        public string PageId { get; set; }
    }
}
