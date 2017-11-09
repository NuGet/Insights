namespace Knapcode.ExplorePackages.Logic
{
    public class HasRegistrationDiscrepancyInOriginalHivePackageQuery : HasRegistrationDiscrepancyPackageQuery
    {
        public HasRegistrationDiscrepancyInOriginalHivePackageQuery(
            ServiceIndexCache serviceIndexCache,
            RegistrationClient registrationService)
            : base(
                  serviceIndexCache,
                  registrationService,
                  PackageQueryNames.HasRegistrationDiscrepancyInOriginalHivePackageQuery,
                  CursorNames.HasRegistrationDiscrepancyInOriginalHivePackageQuery,
                  type: "RegistrationsBaseUrl",
                  hasSemVer2: false)
        {
        }
    }
}
