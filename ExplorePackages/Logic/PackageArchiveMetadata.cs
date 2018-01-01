namespace Knapcode.ExplorePackages.Logic
{
    public class PackageArchiveMetadata
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public long Size { get; set; }
        public long EntryCount { get; set; }
        public long OffsetOfCentralDirectory { get; set; }
        public long? Zip64OffsetOfCentralDirectory { get; set; }
    }
}
