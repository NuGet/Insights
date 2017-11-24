namespace Knapcode.ExplorePackages.Entities
{
    public class CatalogPackageEntity
    {
        public long PackageKey { get; set; }
        public bool Deleted { get; set; }
        public long FirstCommitTimestamp { get; set; }
        public long LastCommitTimestamp { get; set; }

        public PackageEntity Package { get; set; }
    }
}
