// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class CatalogPackageDependencyGroup
    {
        [JsonPropertyName("targetFramework")]
        public string TargetFramework { get; set; }

        [JsonPropertyName("dependencies")]
        public List<CatalogPackageDependency> Dependencies { get; set; }
    }
}
