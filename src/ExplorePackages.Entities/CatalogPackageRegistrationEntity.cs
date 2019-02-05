namespace Knapcode.ExplorePackages.Entities
{
    public class CatalogPackageRegistrationEntity
    {
        public long PackageRegistrationKey { get; set; }
        public long FirstCommitTimestamp { get; set; }
        public long LastCommitTimestamp { get; set; }

        public PackageRegistrationEntity PackageRegistration { get; set; }
    }
}
