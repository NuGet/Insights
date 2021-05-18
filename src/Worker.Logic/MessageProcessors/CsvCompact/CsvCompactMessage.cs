// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights.Worker
{
    public class CsvCompactMessage<T> where T : ICsvRecord
    {
        [JsonProperty("s")]
        public string SourceContainer { get; set; }

        [JsonProperty("b")]
        public int Bucket { get; set; }

        [JsonProperty("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonProperty("f")]
        public bool Force { get; set; }
    }
}
