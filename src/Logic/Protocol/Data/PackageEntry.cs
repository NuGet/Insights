// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageEntry
    {
        [JsonPropertyName("compressedLength")]
        [JsonConverter(typeof(PackageEntryLongConverter))]
        public long CompressedLength { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; }

        [JsonPropertyName("length")]
        [JsonConverter(typeof(PackageEntryLongConverter))]
        public long Length { get; set; }
    }
}
