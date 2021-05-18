// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class RegistrationIndex
    {
        [JsonProperty("items")]
        public List<RegistrationPageItem> Items { get; set; }
    }
}
