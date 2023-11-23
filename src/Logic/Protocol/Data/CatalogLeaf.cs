// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace NuGet.Insights
{
    public class CatalogLeaf : ICatalogLeafItem
    {
        [JsonPropertyName("@id")]
        public string Url { get; set; }

        [JsonPropertyName("@type")]
        [JsonConverter(typeof(CatalogLeafTypeConverter))]
        public CatalogLeafType LeafType { get; set; }

        [JsonPropertyName("catalog:commitId")]
        public string CommitId { get; set; }

        [JsonPropertyName("catalog:commitTimeStamp")]
        public DateTimeOffset CommitTimestamp { get; set; }

        [JsonPropertyName("id")]
        public string PackageId { get; set; }

        [JsonPropertyName("published")]
        public DateTimeOffset Published { get; set; }

        [JsonPropertyName("version")]
        public string PackageVersion { get; set; }
    }
}
