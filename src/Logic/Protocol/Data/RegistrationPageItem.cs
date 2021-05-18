// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class RegistrationPageItem
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("lower")]
        public string Lower { get; set; }

        [JsonProperty("upper")]
        public string Upper { get; set; }

        [JsonProperty("items")]
        public List<RegistrationLeafItem> Items { get; set; }
    }
}
