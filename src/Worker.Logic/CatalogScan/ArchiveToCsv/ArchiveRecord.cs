// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker
{
    public abstract record ArchiveRecord : PackageRecord
    {
        public ArchiveRecord()
        {
        }

        public ArchiveRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = ArchiveResultType.Deleted;
        }

        public ArchiveRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = ArchiveResultType.Available;
        }

        public ArchiveResultType ResultType { get; set; }

        public long Size { get; set; }

        public long OffsetAfterEndOfCentralDirectory { get; set; }
        public uint CentralDirectorySize { get; set; }
        public uint OffsetOfCentralDirectory { get; set; }
        public int EntryCount { get; set; }
        public string Comment { get; set; }
    }
}
