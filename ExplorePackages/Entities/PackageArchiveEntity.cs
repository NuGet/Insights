namespace Knapcode.ExplorePackages.Entities
{
    public class PackageArchiveEntity
    {
        public long PackageKey { get; set; }
        public long Size { get; set; }
        public int EntryCount { get; set; }
        public uint OffsetOfCentralDirectory { get; set; }
        public ulong? Zip64OffsetOfCentralDirectory { get; set; }

        public PackageEntity Package { get; set; }
    }
}
