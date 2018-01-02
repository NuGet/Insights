namespace Knapcode.ExplorePackages.Logic
{
    public class PackageArchiveMetadata
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public long Size { get; set; }
        public int EntryCount { get; set; }
        public uint OffsetOfCentralDirectory { get; set; }
        public ulong? Zip64OffsetOfCentralDirectory { get; set; }
    }
}
