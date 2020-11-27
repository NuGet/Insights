namespace Knapcode.ExplorePackages
{
    public class PackageVisibilityState
    {
        public PackageVisibilityState(bool listed, SemVerType? semVerType)
        {
            Listed = listed;
            SemVerType = semVerType;
        }

        public bool Listed { get; }
        public SemVerType? SemVerType { get; }
    }
}
