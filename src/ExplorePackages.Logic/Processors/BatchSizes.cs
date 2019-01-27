namespace Knapcode.ExplorePackages.Logic
{
    public static class BatchSizes
    {
        public const int DependenciesToDatabase = 1000;
        public const int DependencyPackagesToDatabase_PackageRegistrations = 100;
        public const int DependencyPackagesToDatabase_Packages = 10000;
        public const int MZip = 1000;
        public const int MZipToDatabase = 100;
        public const int PackageDownloadsToDatabase = 1000;
        public const int PackageQueries = 5000;
        public const int PackageQueryService_MatchedPackages = 1000;
        public const int ReprocessCrossCheckDiscrepancies = 5000;
        public const int SearchClient_GetPackageOrNull = 100;
        public const int V2ToDatabase = 100;
    }
}
