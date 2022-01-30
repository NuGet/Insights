// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker
{
    public class CatalogLeafScanMessage
    {
        [JsonPropertyName("s")]
        public string StorageSuffix { get; set; }

        [JsonPropertyName("p0")]
        public string ScanId { get; set; }

        [JsonPropertyName("p1")]
        public string PageId { get; set; }

        [JsonPropertyName("r")]
        public string LeafId { get; set; }
    }
}
