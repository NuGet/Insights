// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;

#nullable enable

namespace NuGet.Insights
{
    public class StorageLeaseResult : BaseLeaseResult<string>
    {
        private StorageLeaseResult(string name, string? leaseId, ETag? etag, bool acquired) : base(leaseId, etag, acquired)
        {
            Name = name;
        }

        public string Name { get; }

        public static StorageLeaseResult Leased(string name, string leaseId, ETag etag)
        {
            return new StorageLeaseResult(name, leaseId, etag, acquired: true);
        }

        public static StorageLeaseResult NotLeased(string name)
        {
            return new StorageLeaseResult(name, leaseId: null, etag: null, acquired: false);
        }
    }
}
