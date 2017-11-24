namespace Knapcode.ExplorePackages.Entities
{
    public class V2PackageEntity
    {
        public long PackageKey { get; set; }
        public long CreatedTimestamp { get; set; }

        public PackageEntity Package { get; set; }
    }
}
