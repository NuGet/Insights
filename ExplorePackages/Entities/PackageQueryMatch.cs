namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQueryMatch
    {
        public long Key { get; set; }
        public int PackageKey { get; set; }
        public int PackageQueryKey { get; set; }
        public Package Package { get; set; }
        public PackageQuery PackageQuery { get; set; }
    }
}
