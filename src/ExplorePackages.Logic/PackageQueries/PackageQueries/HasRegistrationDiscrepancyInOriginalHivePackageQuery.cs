namespace Knapcode.ExplorePackages.Logic
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
