namespace Knapcode.ExplorePackages.Logic
{
    public static class CursorNames
    {
        public static class NuGetOrg
        {
            private const string NuGetOrgPrefix = "NuGet.org, ";
            public const string FlatContainerGlobal = NuGetOrgPrefix + "Flat Container, Global";
            public const string FlatContainerChina = NuGetOrgPrefix + "Flat Container, China";
        }

        public const string CatalogToDatabase = "CatalogToDatabase";
        public const string CatalogToNuspecs = "CatalogToNuspecs";
        public const string FindEmptyIdsNuspecQuery = "FindEmptyIdsNuspecQuery";
        public const string FindRepositoriesNuspecQuery = "FindRepositoriesNuspecQuery";
    }
}
