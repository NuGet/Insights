using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class LeaseResult
    {
        public LeaseResult(LeaseEntity lease, bool acquired)
        {
            Lease = lease;
            Acquired = acquired;
        }

        public LeaseEntity Lease { get; }
        public bool Acquired { get; }
    }
}
