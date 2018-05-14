namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQueryEntity
    {
        public long PackageQueryKey { get; set; }
        public long CursorKey { get; set; }
        public string Name { get; set; }

        public CursorEntity Cursor { get; set; }
    }
}
