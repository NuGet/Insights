// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.SymbolPackageFileToCsv
{
    public partial record SymbolPackageFileRecord : FileRecord, ICsvRecord
    {
        public SymbolPackageFileRecord()
        {
        }

        public SymbolPackageFileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }

        public SymbolPackageFileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }
    }
}
