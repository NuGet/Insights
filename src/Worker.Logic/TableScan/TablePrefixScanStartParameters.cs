// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights.Worker
{
    public class TablePrefixScanStartParameters
    {
        [JsonProperty("sf")]
        public int SegmentsPerFirstPrefix { get; set; }

        [JsonProperty("ss")]
        public int SegmentsPerSubsequentPrefix { get; set; }
    }
}
