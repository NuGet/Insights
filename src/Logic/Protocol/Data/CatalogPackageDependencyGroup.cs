// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class CatalogPackageDependencyGroup
    {
        [JsonProperty("targetFramework")]
        public string TargetFramework { get; set; }

        [JsonProperty("dependencies")]
        public List<CatalogPackageDependency> Dependencies { get; set; }
    }
}
