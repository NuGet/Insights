// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageReadmeToCsv
{
    public partial record PackageReadme : PackageRecord, ICsvRecord
    {
        public PackageReadme()
        {
        }

        public PackageReadme(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageReadmeResultType.Deleted;
        }

        public PackageReadme(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageReadmeResultType.None;
        }

        public PackageReadmeResultType ResultType { get; set; }

        public int? Size { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public string SHA256 { get; set; }
        public string Content { get; set; }
    }
}
