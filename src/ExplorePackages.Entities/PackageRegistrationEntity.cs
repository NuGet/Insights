using System.Collections.Generic;
using System.Diagnostics;

namespace Knapcode.ExplorePackages.Entities
{
    [DebuggerDisplay("PackageRegistration {Id}")]
    public class PackageRegistrationEntity
    {
        public long PackageRegistrationKey { get; set; }
        public string Id { get; set; }

        public CatalogPackageRegistrationEntity CatalogPackageRegistration { get; set; }
        public List<PackageEntity> Packages { get; set; }
        public List<PackageDependencyEntity> PackageDependents { get; set; }
    }
}
