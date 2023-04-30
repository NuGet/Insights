// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker
{
    public class CsvExpandReprocessMessage<T> where T : ICsvRecord
    {
        [JsonPropertyName("b")]
        public int Bucket { get; set; }

        [JsonPropertyName("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonPropertyName("t")]
        public CatalogScanDriverType DriverType { get; set; }

        [JsonPropertyName("i")]
        public string ScanId { get; set; }
    }
}
