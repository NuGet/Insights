// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker
{
    public class HomogeneousBulkEnqueueMessage
    {
        [JsonPropertyName("n")]
        public string SchemaName { get; set; }

        [JsonPropertyName("v")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("d")]
        public TimeSpan? NotBefore { get; set; }

        [JsonPropertyName("m")]
        public List<JsonElement> Messages { get; set; }
    }
}
