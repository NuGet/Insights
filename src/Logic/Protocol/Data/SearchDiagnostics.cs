// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class SearchDiagnostics
    {
        [JsonPropertyName("SearchIndex")]
        public SearchIndexDiagnosticsData SearchIndex { get; set; }

        [JsonPropertyName("HijackIndex")]
        public SearchIndexDiagnosticsData HijackIndex { get; set; }
    }
}
