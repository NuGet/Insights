namespace Knapcode.ExplorePackages.Logic
{
    public static class CursorNames
    {
        public static class NuGetOrg
        {
            private const string NuGetOrgPrefix = "NuGet.org, ";
            public const string FlatContainer = NuGetOrgPrefix + "Flat Container";
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
    }
}
