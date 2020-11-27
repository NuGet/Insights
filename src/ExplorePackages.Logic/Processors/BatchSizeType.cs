namespace Knapcode.ExplorePackages
{
    public enum BatchSizeType
    {
        DependenciesToDatabase,
        DependencyPackagesToDatabase_PackageRegistrations,
        DependencyPackagesToDatabase_Packages,
        MZips,
        MZipToDatabase,
        PackageDownloadsToDatabase,
        PackageQueries,
        PackageQueryService_MatchedPackages,
        ReprocessCrossCheckDiscrepancies,
        V2ToDatabase,
        Nuspecs,
    }
}
