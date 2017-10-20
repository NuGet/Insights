namespace Knapcode.ExplorePackages.Entities
{
    public class Package
    {
        public int Key { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public string Identity { get; set; }
        public bool Deleted { get; set; }
        public byte[] RowVersion { get; set; }
    }
}
