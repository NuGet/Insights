namespace Knapcode.ExplorePackages.Logic
{
    public static class CursorNames
    {
        public static class NuGetOrg
        {
            private const string NuGetOrgPrefix = "NuGet.org, ";
            public const string FlatContainer = NuGetOrgPrefix + "Flat Container";
            public const string Registration = NuGetOrgPrefix + "Registration";
        }

        public const string CatalogToDatabase = "CatalogToDatabase";
        public const string CatalogToNuspecs = "CatalogToNuspecs";

        public const string FindMissingDependencyIdsNuspecQuery = "FindEmptyIdsNuspecQuery";
        public const string FindRepositoriesNuspecQuery = "FindRepositoriesNuspecQuery";
        public const string FindPackageTypesNuspecQuery = "FindPackageTypesNuspecQuery";
        public const string FindInvalidDependencyVersionsNuspecQuery = "FindInvalidDependencyVersionsNuspecQuery";
        public const string FindMissingDependencyVersionsNuspecQuery = "FindMissingDependencyVersionsNuspecQuery";
        public const string FindEmptyDependencyVersionsNuspecQuery = "FindEmptyDependencyVersionsNuspecQuery";
        public const string FindIdsEndingInDotNumberNuspecQuery = "FindIdsEndingInDotNumberNuspecQuery";
        public const string FindFloatingDependencyVersionsNuspecQuery = "FindFloatingDependencyVersionsNuspecQuery";
        public const string FindSemVer2PackageVersionsNuspecQuery = "FindSemVer2PackageVersionsNuspecQuery";
        public const string FindSemVer2DependencyVersionsNuspecQuery = "FindSemVer2DependencyVersionsNuspecQuery";

        public const string HasRegistrationDiscrepancyInOriginalHivePackageQuery = "HasRegistrationDiscrepancyInOriginalHivePackageQuery";
        public const string HasRegistrationDiscrepancyInGzippedHivePackageQuery = "HasRegistrationDiscrepancyInGzippedHivePackageQuery";
        public const string HasRegistrationDiscrepancyInSemVer2HivePackageQuery = "HasRegistrationDiscrepancyInSemVer2HivePackageQuery";
        public const string HasPackagesContainerDiscrepancyPackageQuery = "HasPackagesContainerDiscrepancyPackageQuery";
        public const string HasFlatContainerDiscrepancyPackageQuery = "HasFlatContainerDiscrepancyPackageQuery";
        public const string HasV2DiscrepancyPackageQuery = "HasV2DiscrepancyPackageQuery";
    }
}
