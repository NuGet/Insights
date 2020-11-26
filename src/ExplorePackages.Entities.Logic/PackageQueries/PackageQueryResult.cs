namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryResult
    {
        public PackageQueryResult(IPackageQuery query, PackageIdentity packageIdentity, bool isMatch)
        {
            Query = query;
            PackageIdentity = packageIdentity;
            IsMatch = isMatch;
        }

        public IPackageQuery Query { get; }
        public PackageIdentity PackageIdentity { get; }
        public bool IsMatch { get; }
    }
}
