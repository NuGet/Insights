// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    public partial record PackageIcon : PackageRecord, ICsvRecord
    {
        public PackageIcon()
        {
        }

        public PackageIcon(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }

        public PackageIcon(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }

        public PackageIconResultType ResultType { get; set; }
        public long? FileSize { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public string SHA256 { get; set; }
        public string SHA512 { get; set; }
        public string ContentType { get; set; }

        public string Format { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? FrameCountByTime { get; set; }
        public int? FrameCountByResolution { get; set; }
        public int? FrameCountByPage { get; set; }
        public float? HorizontalResolution { get; set; }
        public float? VerticalResolution { get; set; }
        public int? Flags { get; set; }
        public string PixelFormat { get; set; }

        [KustoType("dynamic")]
        public string PropertyItems { get; set; }
    }
}
