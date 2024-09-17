// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

#nullable enable

namespace NuGet.Insights
{
    public class StorageLeaseService
    {
        public const string MetricIdPrefix = $"{nameof(StorageLeaseService)}.";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly IMetric _acquiredDuration;
        private readonly IMetric _renewedAfter;
        private readonly IMetric _releasedAfter;
        private readonly IMetric _unavailableCount;
        private readonly IMetric _failedRenewCount;
        private readonly IMetric _failedReleaseCount;

        public StorageLeaseService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;

            _acquiredDuration = _telemetryClient.GetMetric($"{MetricIdPrefix}LeaseAcquiredDurationSeconds", "LeaseName");
            _renewedAfter = _telemetryClient.GetMetric($"{MetricIdPrefix}LeaseRenewedAfterSeconds", "LeaseName");
            _releasedAfter = _telemetryClient.GetMetric($"{MetricIdPrefix}LeaseReleasedAfterSeconds", "LeaseName", "IsRecovery");
            _unavailableCount = _telemetryClient.GetMetric($"{MetricIdPrefix}LeaseUnavailable", "LeaseName", "Reason");
            _failedRenewCount = _telemetryClient.GetMetric($"{MetricIdPrefix}LeaseFailedRenew", "LeaseName");
            _failedReleaseCount = _telemetryClient.GetMetric($"{MetricIdPrefix}LeaseFailedRelease", "LeaseName");
        }

        public async Task InitializeAsync()
        {
            await (await GetContainerAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task<StorageLeaseResult> AcquireAsync(string name, TimeSpan leaseDuration)
        {
            return await TryAcquireAsync(name, leaseDuration, shouldThrow: true);
        }

        public async Task<StorageLeaseResult> TryAcquireAsync(string name, TimeSpan leaseDuration)
        {
            return await TryAcquireAsync(name, leaseDuration, shouldThrow: false);
        }

        private async Task<StorageLeaseResult> TryAcquireAsync(string name, TimeSpan leaseDuration, bool shouldThrow)
        {
            var blob = await GetBlobAsync(name);
            var leaseClient = blob.GetBlobLeaseClient();

            DateTimeOffset leaseStarted;
            BlobLease lease;
            try
            {
                try
                {
                    leaseStarted = DateTimeOffset.UtcNow;
                    lease = await leaseClient.AcquireAsync(leaseDuration);
                }
                catch (RequestFailedException acquireEx) when (acquireEx.Status == (int)HttpStatusCode.NotFound)
                {
                    try
                    {
                        await blob.UploadAsync(Stream.Null, overwrite: false);
                    }
                    catch (RequestFailedException createEx) when (createEx.Status == (int)HttpStatusCode.Conflict || createEx.Status == (int)HttpStatusCode.PreconditionFailed)
                    {
                        // Ignore this exception. Another thread created the blob already.
                    }

                    leaseStarted = DateTimeOffset.UtcNow;
                    lease = await leaseClient.AcquireAsync(leaseDuration);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                _unavailableCount.TrackValue(1, name, "AcquireConflict");
                if (shouldThrow)
                {
                    throw new StorageLeaseException(StorageLeaseResult.NotAvailable, ex);
                }
                else
                {
                    return StorageLeaseResult.NotLeased(name);
                }
            }

            // Update the etag so we can detect if anyone else has acquired the lease successfully. This also
            // updates the Last-Modified date which prevents a blob life cycle policy from deleting this blob.
            try
            {
                BlobInfo blobInfo = await blob.SetMetadataAsync(
                    new Dictionary<string, string> { { "leasestarted", leaseStarted.ToString("O") } },
                    new BlobRequestConditions { LeaseId = lease.LeaseId });

                _acquiredDuration.TrackValue(leaseDuration.TotalSeconds, name);
                return StorageLeaseResult.Leased(name, lease.LeaseId, blobInfo.ETag, leaseStarted);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                _unavailableCount.TrackValue(1, name, "UpdatePreconditionFailed");
                if (shouldThrow)
                {
                    throw new StorageLeaseException(StorageLeaseResult.NotAvailable, ex);
                }
                else
                {
                    return StorageLeaseResult.NotLeased(name);
                }
            }
        }

        public async Task RenewAsync(StorageLeaseResult result)
        {
            await TryRenewAsync(result, shouldThrow: true);
        }

        public async Task<bool> TryRenewAsync(StorageLeaseResult result)
        {
            return await TryRenewAsync(result, shouldThrow: false);
        }

        private async Task<bool> TryRenewAsync(StorageLeaseResult result, bool shouldThrow)
        {
            var blob = await GetBlobAsync(result.Name);
            var leaseClient = blob.GetBlobLeaseClient(result.Lease);

            var lastRenewed = DateTimeOffset.UtcNow;
            try
            {
                await leaseClient.RenewAsync();
                result.LastRenewed = lastRenewed;
                _renewedAfter.TrackValue((lastRenewed - result.Started!.Value).TotalSeconds, result.Name);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                _failedRenewCount.TrackValue(1, result.Name);
                if (shouldThrow)
                {
                    throw new StorageLeaseException(StorageLeaseResult.AcquiredBySomeoneElse, ex);
                }
                else
                {
                    return false;
                }
            }
        }

        public async Task BreakAsync(string name)
        {
            var blob = await GetBlobAsync(name);
            var leaseClient = blob.GetBlobLeaseClient();
            await leaseClient.BreakAsync(breakPeriod: TimeSpan.Zero);
        }

        public async Task<bool> TryReleaseAsync(StorageLeaseResult result)
        {
            return await TryReleaseAsync(result, shouldThrow: false);
        }

        public async Task ReleaseAsync(StorageLeaseResult result)
        {
            await TryReleaseAsync(result, shouldThrow: true);
        }

        private async Task<bool> TryReleaseAsync(StorageLeaseResult result, bool shouldThrow)
        {
            var blob = await GetBlobAsync(result.Name);
            var leaseClient = blob.GetBlobLeaseClient(result.Lease);

            DateTimeOffset released = DateTimeOffset.Now;
            try
            {
                await leaseClient.ReleaseAsync();
                result.Released = released;
                _releasedAfter.TrackValue((released - result.Started!.Value).TotalSeconds, result.Name, "false");
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                try
                {
                    BlobProperties properties = await blob.GetPropertiesAsync();
                    if (properties.ETag == result.ETag && properties.LeaseState == LeaseState.Available)
                    {
                        _releasedAfter.TrackValue((released - result.Started!.Value).TotalSeconds, result.Name, "true");
                        return true;
                    }
                }
                catch
                {
                    // Ignore, this is best effort.
                }

                _failedReleaseCount.TrackValue(1, result.Name);
                if (shouldThrow)
                {
                    throw new StorageLeaseException(StorageLeaseResult.AcquiredBySomeoneElse, ex);
                }
                else
                {
                    return false;
                }
            }
        }

        private async Task<BlobClient> GetBlobAsync(string name)
        {
            return (await GetContainerAsync())
                .GetBlobClient(name);
        }

        private async Task<BlobContainerClient> GetContainerAsync()
        {
            return (await _serviceClientFactory.GetBlobServiceClientAsync())
                .GetBlobContainerClient(_options.Value.LeaseContainerName);
        }
    }
}
