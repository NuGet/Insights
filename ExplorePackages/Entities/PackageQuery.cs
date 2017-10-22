namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQuery
    {
        public int Key { get; set; }
        public string CursorName { get; set; }
        public string Name { get; set; }
        public Cursor Cursor { get; set; }
    }
}
