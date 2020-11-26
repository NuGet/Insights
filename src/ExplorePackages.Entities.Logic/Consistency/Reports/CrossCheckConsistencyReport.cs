namespace Knapcode.ExplorePackages.Logic
{
    public class CrossCheckConsistencyReport : IConsistencyReport
    {
        public CrossCheckConsistencyReport(bool isConsistent, bool doPackageContentsMatch)
        {
            IsConsistent = isConsistent;
            DoPackageContentsMatch = doPackageContentsMatch;
        }

        public bool IsConsistent { get; }
        public bool DoPackageContentsMatch { get; }
    }
}
