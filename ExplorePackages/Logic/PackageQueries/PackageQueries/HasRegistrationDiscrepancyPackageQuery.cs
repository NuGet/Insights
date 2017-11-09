using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public abstract class HasRegistrationDiscrepancyPackageQuery : IPackageQuery
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly RegistrationService _registrationService;
        private readonly string _registrationType;
        private readonly bool _hasSemVer2;

        public HasRegistrationDiscrepancyPackageQuery(
            ServiceIndexCache serviceIndexCache,
            RegistrationService registrationService,
            string name,
            string cursorName,
            string registrationType,
            bool hasSemVer2)
        {
            _serviceIndexCache = serviceIndexCache;
            _registrationService = registrationService;
            Name = name;
            CursorName = cursorName;
            _registrationType = registrationType;
            _hasSemVer2 = hasSemVer2;
        }

        public string Name { get; }
        public string CursorName { get; }

        public async Task<bool> IsMatchAsync(PackageQueryContext context)
        {
            var registrationUrl = await _serviceIndexCache.GetUrlAsync(_registrationType);

            var shouldExist = !context.Package.Deleted && (_hasSemVer2 || !context.IsSemVer2);
            var actuallyExists = await _registrationService.HasPackageAsync(
                registrationUrl,
                context.Package.Id,
                context.Package.Version);
            
            return shouldExist != actuallyExists;
        }
    }
}
