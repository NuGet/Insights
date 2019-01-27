namespace Knapcode.ExplorePackages.Logic
{
    public enum BatchSizeType
    {
        DependenciesToDatabase,
        DependencyPackagesToDatabase_PackageRegistrations,
        DependencyPackagesToDatabase_Packages,
        MZip,
        MZipToDatabase,
        PackageDownloadsToDatabase,
        PackageQueries,
        PackageQueryService_MatchedPackages,
        ReprocessCrossCheckDiscrepancies,
        V2ToDatabase,
    }
}
