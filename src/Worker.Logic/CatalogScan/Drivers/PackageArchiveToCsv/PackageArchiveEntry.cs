// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker.PackageArchiveToCsv
{
    public partial record PackageArchiveEntry : ArchiveEntry, ICsvRecord
    {
        public PackageArchiveEntry()
        {
        }

        public PackageArchiveEntry(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public PackageArchiveEntry(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }
    }
}
