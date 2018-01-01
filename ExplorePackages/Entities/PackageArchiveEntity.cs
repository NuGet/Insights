namespace Knapcode.ExplorePackages.Entities
{
    public class PackageArchiveEntity
    {
        public long PackageKey { get; set; }
        public long Size { get; set; }
        public long EntryCount { get; set; }

        public PackageEntity Package { get; set; }
    }
}
