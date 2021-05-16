using System;

namespace NuGet.Insights
{
    public class BaseLeaseResult<T>
    {
        public const string NotAcquiredAtAll = "The provided lease was not acquired in the first place.";
        public const string AcquiredBySomeoneElse = "The lease has been acquired by someone else.";
        public const string NotAvailable = "The lease is not available yet.";

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
