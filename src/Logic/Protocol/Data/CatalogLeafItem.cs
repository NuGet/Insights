// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace NuGet.Insights
{
    public class CatalogLeafItem : ICatalogLeafItem
    {
        [JsonPropertyName("@id")]
        public string Url { get; set; }

        [JsonPropertyName("@type")]
        [JsonConverter(typeof(CatalogLeafItemTypeConverter))]
        public CatalogLeafType Type { get; set; }

        [JsonPropertyName("commitId")]
        public string CommitId { get; set; }

        [JsonPropertyName("commitTimeStamp")]
        public DateTimeOffset CommitTimestamp { get; set; }

        [JsonPropertyName("nuget:id")]
        public string PackageId { get; set; }

        [JsonPropertyName("nuget:version")]
        public string PackageVersion { get; set; }

        DateTimeOffset? IPackageIdentityCommit.CommitTimestamp => CommitTimestamp;
    }
}
