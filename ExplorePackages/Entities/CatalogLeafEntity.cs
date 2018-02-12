namespace Knapcode.ExplorePackages.Entities
{
    public class CatalogLeafEntity
    {
        public long CatalogCommitKey { get; set; }
        public long CatalogLeafKey { get; set; }
        public long PackageKey { get; set; }
        public CatalogLeafType Type { get; set; }

        public CatalogCommitEntity CatalogCommit { get; set; }
        public CatalogPackageEntity CatalogPackage { get; set; }
    }
}
