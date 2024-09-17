// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;

#nullable enable

namespace NuGet.Insights
{
    public class StorageLeaseResult : BaseLeaseResult<string>
    {
        private StorageLeaseResult(string name, string? leaseId, ETag? etag, DateTimeOffset? started, bool acquired)
            : base(leaseId, etag, started, acquired)
        {
            Name = name;
        }

        public string Name { get; }

        public static StorageLeaseResult Leased(string name, string leaseId, ETag etag, DateTimeOffset started)
        {
            return new StorageLeaseResult(name, leaseId, etag, started, acquired: true);
        }

        public static StorageLeaseResult NotLeased(string name)
        {
            return new StorageLeaseResult(name, leaseId: null, etag: null, started: null, acquired: false);
        }
    }
}
