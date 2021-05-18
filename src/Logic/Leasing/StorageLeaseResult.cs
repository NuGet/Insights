// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
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
