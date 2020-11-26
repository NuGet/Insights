using Knapcode.ExplorePackages.Logic;

namespace Knapcode.ExplorePackages.Entities
{
    public class CatalogLeafEntity
    {
        public long CatalogCommitKey { get; set; }
        public long CatalogLeafKey { get; set; }
        public long PackageKey { get; set; }
        public CatalogLeafType Type { get; set; }
        public string RelativePath { get; set; }
        public bool IsListed { get; set; }
        public SemVerType? SemVerType { get; set; }

        public CatalogCommitEntity CatalogCommit { get; set; }
        public CatalogPackageEntity CatalogPackage { get; set; }
    }
}
