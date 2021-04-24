using System;

namespace Knapcode.ExplorePackages.Worker.PackageArchiveToCsv
{
    public partial record PackageArchiveEntry : PackageRecord, ICsvRecord
    {
        public PackageArchiveEntry()
        {
        }

        public PackageArchiveEntry(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public int SequenceNumber { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public int Flags { get; set; }
        public int CompressionMethod { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public long Crc32 { get; set; }
        public long CompressedSize { get; set; }
        public long UncompressedSize { get; set; }
        public long LocalHeaderOffset { get; set; }
        public string Comment { get; set; }
    }
}
