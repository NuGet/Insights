using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class DatabaseLeaseResult : BaseLeaseResult<LeaseEntity>
    {
        private DatabaseLeaseResult(LeaseEntity lease, bool acquired) : base(lease, acquired)
        {
        }

        public static DatabaseLeaseResult Leased(LeaseEntity lease)
        {
            return new DatabaseLeaseResult(lease, acquired: true);
        }

        public static DatabaseLeaseResult NotLeased()
        {
            return new DatabaseLeaseResult(lease: null, acquired: false);
        }
    }
}
