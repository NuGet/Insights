// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker
{
    public abstract record FileRecord : PackageRecord, IPackageEntryRecord
    {
        public FileRecord()
        {
        }

        public FileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = FileRecordResultType.Deleted;
        }

        public FileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = FileRecordResultType.Available;
        }

        [Required]
        public FileRecordResultType ResultType { get; set; }

        public int? SequenceNumber { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public long? CompressedLength { get; set; }
        public long? EntryUncompressedLength { get; set; }

        public long? ActualUncompressedLength { get; set; }
        public string SHA256 { get; set; }
        public string First16Bytes { get; set; }

        protected int CompareTo(FileRecord other)
        {
            var c = base.CompareTo(other);
            if (c != 0)
            {
                return c;
            }

            return Comparer<int?>.Default.Compare(SequenceNumber, other.SequenceNumber);
        }
    }
}
