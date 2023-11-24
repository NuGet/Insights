// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker.CopyBucketRange
{
    public class CopyBucketRangeParameters
    {
        [JsonPropertyName("t")]
        public CatalogScanDriverType DriverType { get; set; }

        [JsonPropertyName("i")]
        public string ScanId { get; set; }
    }
}
