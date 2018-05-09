namespace Knapcode.ExplorePackages.Logic
{
    public class V2ConsistencyReport : IConsistencyReport
    {
        public V2ConsistencyReport(bool isConsistent, bool hasPackageSemVer1, bool hasPackageSemVer2, bool isListedSemVer1, bool isListedSemVer2)
        {
            IsConsistent = isConsistent;
            HasPackageSemVer1 = hasPackageSemVer1;
            HasPackageSemVer2 = hasPackageSemVer2;
            IsListedSemVer1 = isListedSemVer1;
            IsListedSemVer2 = isListedSemVer2;
        }

        public bool IsConsistent { get; }
        public bool HasPackageSemVer1 { get; }
        public bool HasPackageSemVer2 { get; }
        public bool IsListedSemVer1 { get; }
        public bool IsListedSemVer2 { get; }
    }
}
