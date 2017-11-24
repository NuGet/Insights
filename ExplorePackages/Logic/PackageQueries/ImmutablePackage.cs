using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class ImmutablePackage
    {
        private readonly PackageEntity _package;

        public ImmutablePackage(PackageEntity package)
        {
            _package = package;
        }

        public long Key => _package.PackageKey;
        public string Id => _package.PackageRegistration.Id;
        public string Version => _package.Version;
        public string Identity => _package.Identity;
        public bool Deleted => _package.CatalogPackage.Deleted;
        public long FirstCommitTimestamp => _package.CatalogPackage.FirstCommitTimestamp;
        public long LastCommitTimestamp => _package.CatalogPackage.LastCommitTimestamp;
    }
}
