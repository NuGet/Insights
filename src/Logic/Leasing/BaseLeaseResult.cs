// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights
{
    public class BaseLeaseResult<T>
    {
        public const string NotAcquiredAtAll = "The provided lease was not acquired in the first place.";
        public const string AcquiredBySomeoneElse = "The lease has been acquired by someone else, or transient errors happened.";
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
