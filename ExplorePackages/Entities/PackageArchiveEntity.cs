namespace Knapcode.ExplorePackages.Entities
{
    public class PackageArchiveEntity
    {
        public long PackageKey { get; set; }

        public long Size { get; set; }
        public int EntryCount { get; set; }

        public uint CentralDirectorySize { get; set; }
        public byte[] Comment { get; set; }
        public ushort CommentSize { get; set; }
        public ushort DiskWithStartOfCentralDirectory { get; set; }
        public ushort EntriesForWholeCentralDirectory { get; set; }
        public ushort EntriesInThisDisk { get; set; }
        public ushort NumberOfThisDisk { get; set; }
        public long OffsetAfterEndOfCentralDirectory { get; set; }
        public uint OffsetOfCentralDirectory { get; set; }

        public ulong? Zip64CentralDirectorySize { get; set; }
        public uint? Zip64DiskWithStartOfCentralDirectory { get; set; }
        public uint? Zip64DiskWithStartOfEndOfCentralDirectory { get; set; }
        public ulong? Zip64EndOfCentralDirectoryOffset { get; set; }
        public ulong? Zip64EntriesForWholeCentralDirectory { get; set; }
        public ulong? Zip64EntriesInThisDisk { get; set; }
        public uint? Zip64NumberOfThisDisk { get; set; }
        public long? Zip64OffsetAfterEndOfCentralDirectoryLocator { get; set; }
        public ulong? Zip64OffsetOfCentralDirectory { get; set; }
        public uint? Zip64TotalNumberOfDisks { get; set; }
        public ulong? Zip64SizeOfCentralDirectoryRecord { get; set; }
        public ushort? Zip64VersionMadeBy { get; set; }
        public ushort? Zip64VersionToExtract { get; set; }

        public PackageEntity Package { get; set; }
    }
}
