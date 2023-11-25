// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CsvCompactMessage<T> where T : ICsvRecord
    {
        [JsonPropertyName("s")]
        public string SourceContainer { get; set; }

        [JsonPropertyName("b")]
        public int Bucket { get; set; }

        [JsonPropertyName("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonPropertyName("f")]
        public bool Force { get; set; }
    }
}
