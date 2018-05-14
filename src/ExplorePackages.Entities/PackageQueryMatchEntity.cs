namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQueryMatchEntity
    {
        public long PackageQueryMatchKey { get; set; }
        public long PackageKey { get; set; }
        public long PackageQueryKey { get; set; }

        public PackageEntity Package { get; set; }
        public PackageQueryEntity PackageQuery { get; set; }
    }
}
