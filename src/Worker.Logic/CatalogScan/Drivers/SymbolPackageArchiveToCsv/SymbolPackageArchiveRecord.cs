// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker.SymbolPackageArchiveToCsv
{
    public partial record SymbolPackageArchiveRecord : ArchiveRecord, ICsvRecord
    {
        public SymbolPackageArchiveRecord()
        {
        }

        public SymbolPackageArchiveRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public SymbolPackageArchiveRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public string HeaderMD5 { get; set; }
        public string HeaderSHA512 { get; set; }
    }
}
