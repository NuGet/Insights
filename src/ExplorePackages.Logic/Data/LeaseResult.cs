using System;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class LeaseResult
    {
        private LeaseResult(LeaseEntity lease, bool acquired)
        {
            if (acquired && lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            if (!acquired && lease != null)
            {
                throw new ArgumentException("The lease must be null if the it was not acquired.", nameof(lease));
            }

            Lease = lease;
            Acquired = acquired;
        }

        public LeaseEntity Lease { get; }
        public bool Acquired { get; }

        public static LeaseResult Leased(LeaseEntity lease)
        {
            return new LeaseResult(lease, acquired: true);
        }

        public static LeaseResult NotLeased()
        {
            return new LeaseResult(lease: null, acquired: false);
        }
    }
}
