// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class TablePrefixScanPrefixQueryParameters
    {
        [JsonPropertyName("sf")]
        public int SegmentsPerFirstPrefix { get; set; }

        [JsonPropertyName("ss")]
        public int SegmentsPerSubsequentPrefix { get; set; }

        [JsonPropertyName("d")]
        public int Depth { get; set; }

        [JsonPropertyName("p")]
        public string PartitionKeyPrefix { get; set; }

        [JsonPropertyName("m")]
        public string PartitionKeyLowerBound { get; set; }

        [JsonPropertyName("u")]
        public string PartitionKeyUpperBound { get; set; }
    }
}
