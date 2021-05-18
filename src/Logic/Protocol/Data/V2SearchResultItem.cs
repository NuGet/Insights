// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class V2SearchResultItem
    {
        [JsonProperty("PackageRegistration")]
        public V2SearchResultPackageRegistration PackageRegistration { get; set; }

        [JsonProperty("Listed")]
        public bool Listed { get; set; }
    }
}
