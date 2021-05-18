// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public class HomogeneousBatchMessage
    {
        [JsonProperty("n")]
        public string SchemaName { get; set; }

        [JsonProperty("v")]
        public int SchemaVersion { get; set; }

        [JsonProperty("m")]
        public List<JToken> Messages { get; set; }
    }
}
