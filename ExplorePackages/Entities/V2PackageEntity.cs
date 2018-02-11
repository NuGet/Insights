namespace Knapcode.ExplorePackages.Entities
{
    public class V2PackageEntity
    {
        public long PackageKey { get; set; }
        public long CreatedTimestamp { get; set; }
        public long? LastEditedTimestamp { get; set; }
        public long PublishedTimestamp { get; set; }
        public long LastUpdatedTimestamp { get; set; }
        public bool Listed { get; set; }

        public PackageEntity Package { get; set; }
    }
}
