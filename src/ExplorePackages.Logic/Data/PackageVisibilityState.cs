namespace Knapcode.ExplorePackages.Logic
{
    public class PackageVisibilityState
    {
        public PackageVisibilityState(bool? listed, bool? semVer2)
        {
            Listed = listed;
            SemVer2 = semVer2;
        }

        public bool? Listed { get; }
        public bool? SemVer2 { get; }
    }
}
