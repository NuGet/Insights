namespace Knapcode.ExplorePackages.Logic
{
    public class HasRegistrationDiscrepancyInOriginalHivePackageQuery : HasRegistrationDiscrepancyPackageQuery
    {
        public HasRegistrationDiscrepancyInOriginalHivePackageQuery(
            ServiceIndexCache serviceIndexCache,
            RegistrationService registrationService)
            : base(
                  serviceIndexCache,
                  registrationService,
                  PackageQueryNames.HasRegistrationDiscrepancyInOriginalHivePackageQuery,
                  CursorNames.HasRegistrationDiscrepancyInOriginalHivePackageQuery,
                  registrationType: "RegistrationsBaseUrl",
                  hasSemVer2: false)
        {
        }
    }
}
