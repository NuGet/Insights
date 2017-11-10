namespace Knapcode.ExplorePackages.Logic
{
    public class FlatContainerConsistencyReport : IConsistencyReport
    {
        public FlatContainerConsistencyReport(
            bool isConsistent,
            bool hasPackageContent,
            bool hasPackageManifest,
            bool isInIndex)
        {
            IsConsistent = isConsistent;
            HasPackageContent = hasPackageContent;
            HasPackageManifest = hasPackageManifest;
            IsInIndex = isInIndex;
        }

        public bool IsConsistent { get; }
        public bool HasPackageContent { get; }
        public bool HasPackageManifest { get; }
        public bool IsInIndex { get; }
    }
}
