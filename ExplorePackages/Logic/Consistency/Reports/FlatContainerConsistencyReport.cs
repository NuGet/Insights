namespace Knapcode.ExplorePackages.Logic
{
    public class FlatContainerConsistencyReport : IConsistencyReport
    {
        public FlatContainerConsistencyReport(
           bool isConsistent,
           bool hasPackageContent, 
           bool hasPackageManifest,
           bool isInIndex,
           string packagesContainerMd5, 
           string flatContainerMd5)
        {
            IsConsistent = isConsistent;
            HasPackageContent = hasPackageContent;
            HasPackageManifest = hasPackageManifest;
            IsInIndex = isInIndex;
            PackagesContainerMd5 = packagesContainerMd5;
            FlatContainerMd5 = flatContainerMd5;
        }

        public bool IsConsistent { get; }
        public bool HasPackageContent { get; }
        public bool HasPackageManifest { get; }
        public bool IsInIndex { get; }
        public string PackagesContainerMd5 { get; set; }
        public string FlatContainerMd5 { get; set; }
    }
}
