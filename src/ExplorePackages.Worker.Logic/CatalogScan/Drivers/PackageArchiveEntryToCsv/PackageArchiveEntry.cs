using System;

namespace Knapcode.ExplorePackages.Worker.PackageArchiveEntryToCsv
{
    public partial record PackageArchiveEntry : PackageRecord, ICsvRecord<PackageArchiveEntry>
    {
        public PackageArchiveEntry()
        {
        }

        public PackageArchiveEntry(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageArchiveEntryResultType.Deleted;
        }

        public PackageArchiveEntry(
            Guid? scanId,
            DateTimeOffset? scanTimestamp,
            PackageDetailsCatalogLeaf leaf,
            PackageArchiveEntryResultType resultType)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = resultType;
        }

        public PackageArchiveEntryResultType ResultType { get; set; }

        public int SequenceNumber { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public long UncompressedSize { get; set; }
        public long Crc32 { get; set; }
    }
}
