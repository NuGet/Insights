// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class CatalogPackageDependency
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("range")]
        [JsonConverter(typeof(CatalogPackageDependencyRangeConverter))]
        public string Range { get; set; }
    }
}
