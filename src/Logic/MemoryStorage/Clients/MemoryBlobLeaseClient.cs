// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryBlobLeaseClient : BlobLeaseClient
    {
        public MemoryBlobLeaseClient(MemoryBlobClient client, string? leaseId)
            : base(client, leaseId)
        {
            Options = client.Options;
            Parent = client;
        }

        public BlobClientOptions Options { get; }
        public MemoryBlobClient Parent { get; }

        public override Task<Response<BlobLease>> AcquireAsync(
            TimeSpan duration,
            RequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Parent.Store.AcquireLeaseResponse(duration, conditions));
        }

        public override Task<Response<BlobLease>> BreakAsync(
            TimeSpan? breakPeriod = null,
            RequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Parent.Store.BreakLeaseResponse(breakPeriod, conditions));
        }

        public override Task<Response<ReleasedObjectInfo>> ReleaseAsync(
            RequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Parent.Store.ReleaseLeaseResponse(LeaseId, conditions));
        }

        public override Task<Response<BlobLease>> RenewAsync(
            RequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Parent.Store.RenewLeaseResponse(LeaseId, conditions));
        }
    }
}
