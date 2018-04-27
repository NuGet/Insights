using System.Collections.Generic;
using System.Diagnostics;

namespace Knapcode.ExplorePackages.Entities
{
    [DebuggerDisplay("Package {Identity}")]
    public class PackageEntity
    {
        private string _id;
        private string _idIdentity;

        public long PackageRegistrationKey { get; set; }
        public long PackageKey { get; set; }
        public string Version { get; set; }
        public string Identity { get; set; }

        public string Id
        {
            get
            {
                if (PackageRegistration != null)
                {
                    return PackageRegistration.Id;
                }

                if (_idIdentity == Identity)
                {
                    return _id;
                }

                if (Identity == null)
                {
                    return null;
                }

                var pieces = Identity.Split(new char[] { '/' }, 2);
                if (pieces.Length != 2)
                {
                    return null;
                }

                _id = pieces[0];
                _idIdentity = Identity;

                return _id;
            }
        }

        public PackageRegistrationEntity PackageRegistration { get; set; }
        public V2PackageEntity V2Package { get; set; }
        public CatalogPackageEntity CatalogPackage { get; set; }
        public PackageDownloadsEntity PackageDownloads { get; set; }
        public PackageArchiveEntity PackageArchive { get; set; }

        public List<PackageQueryMatchEntity> PackageQueryMatches { get; set; }
        public List<PackageDependencyEntity> PackageDependencies { get; set; }
        public List<PackageDependencyEntity> MinimumPackageDependents { get; set; }
        public List<PackageDependencyEntity> BestPackageDependents { get; set; }
    }
}
