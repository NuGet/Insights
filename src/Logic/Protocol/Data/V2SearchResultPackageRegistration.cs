// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class V2SearchResultPackageRegistration
    {
        [JsonProperty("Id")]
        public string Id { get; set; }
    }
}
