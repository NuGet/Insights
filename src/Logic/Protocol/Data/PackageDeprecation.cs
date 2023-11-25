// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageDeprecation
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("reasons")]
        public List<string> Reasons { get; set; }

        [JsonPropertyName("alternatePackage")]
        public PackageDeprecationAlternatePackage AlternatePackage { get; set; }
    }
}
