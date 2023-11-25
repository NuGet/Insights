// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class RegistrationPageItem
    {
        [JsonPropertyName("@id")]
        public string Url { get; set; }

        [JsonPropertyName("lower")]
        public string Lower { get; set; }

        [JsonPropertyName("upper")]
        public string Upper { get; set; }

        [JsonPropertyName("items")]
        public List<RegistrationLeafItem> Items { get; set; }
    }
}
