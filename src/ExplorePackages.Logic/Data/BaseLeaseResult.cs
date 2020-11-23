using System;

namespace Knapcode.ExplorePackages.Logic
{
    public class BaseLeaseResult<T>
    {
        protected BaseLeaseResult(T lease, bool acquired)
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

        public T Lease { get; }
        public bool Acquired { get; }
    }
}
