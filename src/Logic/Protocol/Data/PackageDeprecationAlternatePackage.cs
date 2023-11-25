// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageDeprecationAlternatePackage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("range")]
        public string Range { get; set; }
    }
}
