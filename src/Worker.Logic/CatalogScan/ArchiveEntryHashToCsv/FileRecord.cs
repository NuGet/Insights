// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public enum FileHashResultType
    {
        Available,
        Deleted,
        DoesNotExist,
        InvalidZipEntry,
    }

    public abstract record FileRecord : PackageRecord
    {
        public FileRecord()
        {
        }

        public FileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = FileHashResultType.Deleted;
        }

        public FileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = FileHashResultType.Available;
        }

        public FileHashResultType ResultType { get; set; }

        public int? SequenceNumber { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public long? CompressedLength { get; set; }
        public long? EntryUncompressedLength { get; set; }

        public long? ActualUncompressedLength { get; set; }

        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public string SHA256 { get; set; }
        public string SHA512 { get; set; }

        public string First64 { get; set; }
    }
}
