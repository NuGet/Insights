namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationConsistencyReport : IConsistencyReport
    {
        public RegistrationConsistencyReport(bool isConsistent, bool isInIndex, bool hasLeaf, bool isListedInIndex, bool isListedInLeaf)
        {
            IsConsistent = isConsistent;
            IsInIndex = isInIndex;
            HasLeaf = hasLeaf;
            IsListedInIndex = isListedInIndex;
            IsListedInLeaf = isListedInLeaf;
        }

        public bool IsConsistent { get; }
        public bool IsInIndex { get; }
        public bool HasLeaf { get; }
        public bool IsListedInIndex { get; }
        public bool IsListedInLeaf { get; }
    }
}
