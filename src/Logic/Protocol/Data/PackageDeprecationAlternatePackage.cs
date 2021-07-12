﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class PackageDeprecationAlternatePackage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("range")]
        public string Range { get; set; }
    }
}
