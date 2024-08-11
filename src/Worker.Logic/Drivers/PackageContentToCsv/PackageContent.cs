// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageContentToCsv
{
    public partial record PackageContent : PackageRecord, ICsvRecord
    {
        public PackageContent()
        {
        }

        public PackageContent(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageContentResultType.Deleted;
        }

        public PackageContent(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageContentResultType resultType)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = resultType;
        }

        public PackageContentResultType ResultType { get; set; }

        public string Path { get; set; }
        public string FileExtension { get; set; }
        public int? SequenceNumber { get; set; }
        public int? Size { get; set; }
        public bool? Truncated { get; set; }
        public int? TruncatedSize { get; set; }
        public string SHA256 { get; set; }
        public string Content { get; set; }
        public bool? DuplicateContent { get; set; }
    }
}
