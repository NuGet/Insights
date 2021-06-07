// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker.PackageArchiveToCsv
{
    public partial record PackageArchiveEntry : PackageRecord, ICsvRecord
    {
        public PackageArchiveEntry()
        {
        }

        public PackageArchiveEntry(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageArchiveResultType.Deleted;
        }

        public PackageArchiveEntry(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageArchiveResultType.Available;
        }

        public PackageArchiveResultType ResultType { get; set; }

        public int SequenceNumber { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public ushort Flags { get; set; }
        public ushort CompressionMethod { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public uint Crc32 { get; set; }
        public uint CompressedSize { get; set; }
        public uint UncompressedSize { get; set; }
        public uint LocalHeaderOffset { get; set; }
        public string Comment { get; set; }
    }
}
