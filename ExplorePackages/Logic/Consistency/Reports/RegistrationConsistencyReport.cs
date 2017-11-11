namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationConsistencyReport : IConsistencyReport
    {
        public RegistrationConsistencyReport(bool isConsistent, bool isInIndex, bool hasLeaf)
        {
            IsConsistent = isConsistent;
            IsInIndex = isInIndex;
            HasLeaf = hasLeaf;
        }

        public bool IsConsistent { get; }
        public bool IsInIndex { get; }
        public bool HasLeaf { get; }
    }
}
