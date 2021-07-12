// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class PackageDeprecation
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("reasons")]
        public List<string> Reasons { get; set; }

        [JsonProperty("alternatePackage")]
        public PackageDeprecationAlternatePackage AlternatePackage { get; set; }
    }
}
