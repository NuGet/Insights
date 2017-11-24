namespace Knapcode.ExplorePackages.Entities
{
    public class PackageDownloadsEntity
    {
        public long PackageKey { get; set; }
        public long Downloads { get; set; }

        public PackageEntity Package { get; set; }
    }
}
