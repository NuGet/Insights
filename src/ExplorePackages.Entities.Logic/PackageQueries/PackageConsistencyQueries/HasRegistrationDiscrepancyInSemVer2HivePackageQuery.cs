namespace Knapcode.ExplorePackages.Entities
{
    public class HasRegistrationDiscrepancyInSemVer2HivePackageQuery : HasRegistrationDiscrepancyPackageQuery
    {
        public HasRegistrationDiscrepancyInSemVer2HivePackageQuery(
            RegistrationSemVer2ConsistencyService service)
            : base(
                  service,
                  PackageQueryNames.HasRegistrationDiscrepancyInSemVer2HivePackageQuery,
                  CursorNames.HasRegistrationDiscrepancyInSemVer2HivePackageQuery)
        {
        }
    }
}
