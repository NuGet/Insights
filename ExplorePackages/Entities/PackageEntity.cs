namespace Knapcode.ExplorePackages.Entities
{
    public class PackageEntity
    {
        public long PackageRegistrationKey { get; set; }
        public long PackageKey { get; set; }
        public string Version { get; set; }
        public string Identity { get; set; }

        public PackageRegistrationEntity PackageRegistration { get; set; }
        public V2PackageEntity V2Package { get; set; }
        public CatalogPackageEntity CatalogPackage { get; set; }
    }
}
