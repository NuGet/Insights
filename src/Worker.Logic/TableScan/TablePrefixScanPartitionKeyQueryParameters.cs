// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class TablePrefixScanPartitionKeyQueryParameters
    {
        [JsonPropertyName("sf")]
        public int SegmentsPerFirstPrefix { get; set; }

        [JsonPropertyName("ss")]
        public int SegmentsPerSubsequentPrefix { get; set; }

        [JsonPropertyName("d")]
        public int Depth { get; set; }

        [JsonPropertyName("p")]
        public string PartitionKey { get; set; }

        [JsonPropertyName("r")]
        public string RowKeySkip { get; set; }
    }
}
