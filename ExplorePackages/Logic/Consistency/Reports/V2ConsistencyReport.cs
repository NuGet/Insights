namespace Knapcode.ExplorePackages.Logic
{
    public class V2ConsistencyReport : IConsistencyReport
    {
        public V2ConsistencyReport(bool isConsistent, bool hasPackageSemVer1, bool hasPackageSemVer2)
        {
            IsConsistent = isConsistent;
            HasPackageSemVer1 = hasPackageSemVer1;
            HasPackageSemVer2 = hasPackageSemVer2;
        }

        public bool IsConsistent { get; }
        public bool HasPackageSemVer1 { get; }
        public bool HasPackageSemVer2 { get; }
    }
}
