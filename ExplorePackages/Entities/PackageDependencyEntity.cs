namespace Knapcode.ExplorePackages.Entities
{
    public class PackageDependencyEntity
    {
        public long PackageDependencyKey { get; set; }
        public long ParentPackageKey { get; set; }
        public long DependencyPackageRegistrationKey { get; set; }
        public long? FrameworkKey { get; set; }
        public string VersionRange { get; set; }
        public string OriginalVersionRange { get; set; }
        public long? MinimumDependencyPackageKey { get; set; }
        public long? BestDependencyPackageKey { get; set; }

        public PackageEntity ParentPackage { get; set; }
        public PackageRegistrationEntity DependencyPackageRegistration { get; set; }
        public FrameworkEntity Framework { get; set; }
        public PackageEntity MinimumDependencyPackage { get; set; }
        public PackageEntity BestDependencyPackage { get; set; }
    }
}
