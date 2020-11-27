namespace Knapcode.ExplorePackages.Entities
{
    public class HasRegistrationDiscrepancyInOriginalHivePackageQuery : HasRegistrationDiscrepancyPackageQuery
    {
        public HasRegistrationDiscrepancyInOriginalHivePackageQuery(
            RegistrationOriginalConsistencyService service)
            : base(
                  service,
                  PackageQueryNames.HasRegistrationDiscrepancyInOriginalHivePackageQuery,
                  CursorNames.HasRegistrationDiscrepancyInOriginalHivePackageQuery)
        {
        }
    }
}
