namespace Knapcode.ExplorePackages.Logic
{
    public class PackagesContainerConsistencyReport : IConsistencyReport
    {
        public PackagesContainerConsistencyReport(bool isConsistent, bool hasPackageContent)
        {
            IsConsistent = isConsistent;
            HasPackageContent = hasPackageContent;
        }

        public bool IsConsistent { get; }
        public bool HasPackageContent { get; }
    }
}
