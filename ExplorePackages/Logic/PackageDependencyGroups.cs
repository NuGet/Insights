namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDependencyGroups
    {
        public PackageDependencyGroups(PackageIdentity identity, DependencyGroups dependencyGroups)
        {
            Identity = identity;
            DependencyGroups = dependencyGroups;
        }

        public PackageIdentity Identity { get; }
        public DependencyGroups DependencyGroups { get; }
    }
}
