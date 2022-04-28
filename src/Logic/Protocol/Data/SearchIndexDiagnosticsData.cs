// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace NuGet.Insights
{
    public class SearchIndexDiagnosticsData
    {
        [JsonPropertyName("LastCommitTimestamp")]
        public DateTimeOffset LastCommitTimestamp { get; set; }
    }
}
