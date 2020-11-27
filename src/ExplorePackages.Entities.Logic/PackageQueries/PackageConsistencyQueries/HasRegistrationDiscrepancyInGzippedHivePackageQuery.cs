namespace Knapcode.ExplorePackages.Entities
{
    public class HasRegistrationDiscrepancyInGzippedHivePackageQuery : HasRegistrationDiscrepancyPackageQuery
    {
        public HasRegistrationDiscrepancyInGzippedHivePackageQuery(RegistrationGzippedConsistencyService service)
            : base(
                  service,
                  PackageQueryNames.HasRegistrationDiscrepancyInGzippedHivePackageQuery,
                  CursorNames.HasRegistrationDiscrepancyInGzippedHivePackageQuery)
        {
        }
    }
}
