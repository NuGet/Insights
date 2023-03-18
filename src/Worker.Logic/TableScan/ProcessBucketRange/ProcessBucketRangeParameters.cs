// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker.ProcessBucketRange
{
    public class ProcessBucketRangeParameters
    {
        [JsonPropertyName("t")]
        public CatalogScanDriverType DriverType { get; set; }

        [JsonPropertyName("i")]
        public string ScanId { get; set; }

        [JsonPropertyName("e")]
        public bool Enqueue { get; set; }
    }
}
