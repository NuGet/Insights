namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQueryMatch
    {
        public int Key { get; set; }
        public int PackageKey { get; set; }
        public int PackageQueryKey { get; set; }
        public Package Package { get; set; }
        public PackageQuery PackageQuery { get; set; }
    }
}
