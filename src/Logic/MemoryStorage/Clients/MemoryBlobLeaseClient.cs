// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryBlobLeaseClient : BlobLeaseClient
    {
        private readonly MemoryBlobStore _store;

        public MemoryBlobLeaseClient(MemoryBlobStore store, MemoryBlobClient client, string? leaseId)
            : base(client, leaseId)
        {
            _store = store;
        }

        public override Task<Response<BlobLease>> AcquireAsync(
            TimeSpan duration,
            RequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.AcquireLeaseResponse(duration, conditions));
        }

        public override Task<Response<BlobLease>> BreakAsync(
            TimeSpan? breakPeriod = null,
            RequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.BreakLeaseResponse(breakPeriod, conditions));
        }

        public override Task<Response<ReleasedObjectInfo>> ReleaseAsync(
            RequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.ReleaseLeaseResponse(LeaseId, conditions));
        }

        public override Task<Response<BlobLease>> RenewAsync(
            RequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.RenewLeaseResponse(LeaseId, conditions));
        }
    }
}
