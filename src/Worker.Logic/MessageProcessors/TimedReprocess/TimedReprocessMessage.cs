// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessMessage
    {
        [JsonPropertyName("i")]
        public string RunId { get; set; }

        [JsonPropertyName("ac")]
        public int AttemptCount { get; set; }
    }
}
