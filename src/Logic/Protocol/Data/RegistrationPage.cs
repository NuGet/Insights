// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class RegistrationPage
    {
        [JsonProperty("items")]
        public List<RegistrationLeafItem> Items { get; set; }
    }
}
