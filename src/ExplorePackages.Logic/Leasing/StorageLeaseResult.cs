namespace Knapcode.ExplorePackages
{
    public class StorageLeaseResult : BaseLeaseResult<string>
    {
        private StorageLeaseResult(string name, string leaseId, bool acquired) : base(leaseId, acquired)
        {
            Name = name;
        }

        public string Name { get; }

        public static StorageLeaseResult Leased(string name, string leaseId)
        {
            return new StorageLeaseResult(name, leaseId, acquired: true);
        }

        public static StorageLeaseResult NotLeased()
        {
            return new StorageLeaseResult(name: null, leaseId: null, acquired: false);
        }
    }
}
