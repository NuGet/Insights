// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<TimedReprocessStorageService> _logger;

        public TimedReprocessStorageService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<TimedReprocessStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            var table = await GetTableAsync();
            await table.CreateIfNotExistsAsync(retry: true);

            var buckets = await GetBucketsWithoutValidationAsync(table);
            var missingBuckets = Enumerable
                .Range(0, BucketedPackage.BucketCount)
                .Except(buckets.Select(x => x.Index))
                .ToList();
            var lastReprocessed = GetInitializeLastProcessed(DateTimeOffset.UtcNow, _options.Value.TimedReprocessWindow);

            if (missingBuckets.Any())
            {
                var batch = new MutableTableTransactionalBatch(table);
                foreach (var index in missingBuckets)
                {
                    batch.AddEntity(new TimedReprocessBucket(index) { LastProcessed = lastReprocessed });
                    if (batch.Count >= StorageUtility.MaxBatchSize)
                    {
                        await batch.SubmitBatchAsync();
                        batch = new MutableTableTransactionalBatch(table);
                    }
                }

                await batch.SubmitBatchIfNotEmptyAsync();
            }

            if (missingBuckets.Count + buckets.Count > BucketedPackage.BucketCount)
            {
                throw new InvalidOperationException("There are extra buckets to reprocess. Perhaps the table schema has changed.");
            }
        }

        private static DateTimeOffset GetInitializeLastProcessed(DateTimeOffset created, TimeSpan window)
        {
            return created - (2 * window);
        }

        public async Task<TimedReprocessRun> GetRunAsync(string runId)
        {
            var table = await GetTableAsync();
            return await table.GetEntityOrNullAsync<TimedReprocessRun>(TimedReprocessRun.DefaultPartitionKey, runId);
        }

        public async Task AddRunAsync(TimedReprocessRun run)
        {
            var table = await GetTableAsync();
            var response = await table.AddEntityAsync(run);
            run.UpdateETag(response);
        }

        public async Task ReplaceRunAsync(TimedReprocessRun run)
        {
            _logger.LogInformation("Updating timed reprocess run {RunId} with state {State}.", run.RunId, run.State);

            var table = await GetTableAsync();
            var response = await table.UpdateEntityAsync(run, run.ETag, TableUpdateMode.Replace);
            run.UpdateETag(response);
        }

        public async Task<IReadOnlyList<TimedReprocessRun>> GetRunsAsync()
        {
            var table = await GetTableAsync();
            return await table
                .QueryAsync<TimedReprocessRun>(x => x.PartitionKey == TimedReprocessRun.DefaultPartitionKey)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<TimedReprocessRun>> GetLatestRunsAsync(int maxEntities)
        {
            return await(await GetTableAsync())
                .QueryAsync<TimedReprocessRun>(x => x.PartitionKey == TimedReprocessRun.DefaultPartitionKey)
                .Take(maxEntities)
                .ToListAsync();
        }

        public async Task<List<TimedReprocessCatalogScan>> GetScansAsync(string runId)
        {
            var table = await GetTableAsync();
            return await table
                .QueryAsync<TimedReprocessCatalogScan>(x => x.PartitionKey == TimedReprocessCatalogScan.GetPartitionKey(runId))
                .ToListAsync();
        }

        public async Task<TimedReprocessCatalogScan> GetOrAddScanAsync(TimedReprocessCatalogScan scan)
        {
            var table = await GetTableAsync();
            try
            {
                var response = await table.AddEntityAsync(scan);
                scan.UpdateETag(response);
                return scan;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                return await table.GetEntityAsync<TimedReprocessCatalogScan>(scan.PartitionKey, scan.RowKey);
            }
        }

        public async Task ReplaceScanAsync(TimedReprocessCatalogScan scan)
        {
            _logger.LogInformation(
                "Updating timed catalog scan {DriverType} for timed reprocess run {RunId} to completed = {Completed}.",
                scan.DriverType,
                scan.RunId,
                scan.Completed);

            var table = await GetTableAsync();
            var response = await table.UpdateEntityAsync(scan, scan.ETag, TableUpdateMode.Replace);
            scan.UpdateETag(response);
        }

        public async Task DeleteOldRunsAsync(string currentRunId)
        {
            var table = await GetTableAsync();
            var oldRuns = await table
                .QueryAsync<TimedReprocessRun>(x => x.PartitionKey == TimedReprocessRun.DefaultPartitionKey
                                                 && x.RowKey.CompareTo(currentRunId) > 0)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());

            var oldRunsToDelete = oldRuns
                .OrderByDescending(x => x.Created)
                .Skip(_options.Value.OldWorkflowRunsToKeep)
                .OrderBy(x => x.Created)
                .Where(x => x.State == TimedReprocessState.Complete)
                .ToList();
            _logger.LogInformation("Deleting {Count} old timed reprocess runs.", oldRunsToDelete.Count);

            var batch = new MutableTableTransactionalBatch(table);

            // delete scans
            foreach (var run in oldRunsToDelete)
            {
                var scans = await GetScansAsync(run.RunId);
                foreach (var scan in scans)
                {
                    batch.DeleteEntity(scan.PartitionKey, scan.RowKey, scan.ETag);
                    await batch.SubmitBatchIfFullAsync();
                }
            }

            await batch.SubmitBatchIfNotEmptyAsync();

            // delete runs
            foreach (var run in oldRunsToDelete)
            {
                batch.DeleteEntity(run.PartitionKey, run.RowKey, run.ETag);
                await batch.SubmitBatchIfFullAsync();
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        public async Task<IReadOnlyList<int>> GetAllStaleBucketsAsync()
        {
            var details = TimedReprocessDetails.Create(_options.Value.TimedReprocessWindow, BucketedPackage.BucketCount);

            var bucketsToProcess = await GetBucketsToReprocessAsync(details);

            return bucketsToProcess.Select(x => x.Index).ToList();
        }

        public async Task<List<TimedReprocessBucket>> GetBucketsToReprocessAsync()
        {
            var details = TimedReprocessDetails.Create(_options.Value);

            var bucketsToProcess = await GetBucketsToReprocessAsync(details);

            _logger.LogInformation("Next buckets to process: {Buckets}, using details {Details}", bucketsToProcess.Select(x => x.Index), details);

            return bucketsToProcess;
        }

        private async Task<List<TimedReprocessBucket>> GetBucketsToReprocessAsync(TimedReprocessDetails details)
        {
            var table = await GetTableAsync();
            var buckets = await GetBucketsAsync(table);

            var bucketsToProcess = new List<TimedReprocessBucket>();
            var added = new HashSet<int>();

            // Add unprocessed or late buckets.
            foreach (var bucket in buckets.OrderBy(x => x.LastProcessed))
            {
                if (bucket.LastProcessed + details.Window < details.WindowStart)
                {
                    bucketsToProcess.Add(bucket);
                    added.Add(bucket.Index);
                }
                else
                {
                    break;
                }

                if (bucketsToProcess.Count >= details.MaxBuckets)
                {
                    break;
                }
            }

            // Add buckets that are up to the current time's bucket.
            if (bucketsToProcess.Count < details.MaxBuckets)
            {
                foreach (var bucket in buckets)
                {
                    if (bucket.LastProcessed + details.Window < details.WindowEnd &&
                        bucket.Index <= details.CurrentBucket)
                    {
                        if (added.Add(bucket.Index))
                        {
                            bucketsToProcess.Add(bucket);
                        }
                    }

                    if (bucketsToProcess.Count >= details.MaxBuckets)
                    {
                        break;
                    }
                }
            }

            return bucketsToProcess;
        }

        public async Task ResetBucketsAsync()
        {
            var table = await GetTableAsync();
            var buckets = await GetBucketsWithoutValidationAsync(table);

            var lastReprocessed = GetInitializeLastProcessed(DateTimeOffset.UtcNow, _options.Value.TimedReprocessWindow);
            var batch = new MutableTableTransactionalBatch(table);

            foreach (var bucket in buckets)
            {
                bucket.LastProcessed = lastReprocessed;
                batch.UpdateEntity(bucket, ETag.All, TableUpdateMode.Replace);
                await batch.SubmitBatchIfFullAsync();
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        public async Task MarkBucketsAsProcessedAsync(IEnumerable<int> buckets)
        {
            await MarkBucketsAsProcessedAsync(buckets, _options.Value.TimedReprocessWindow);
        }

        public async Task MarkBucketsAsProcessedAsync(IEnumerable<int> buckets, TimeSpan lastProcessedDelta)
        {
            var indexToBucket = (await GetBucketsAsync()).ToDictionary(x => x.Index);
            var bucketEntities = buckets.Select(x => indexToBucket[x]).ToList();

            var table = await GetTableAsync();
            var batch = new MutableTableTransactionalBatch(table);
            foreach (var bucket in bucketEntities)
            {
                bucket.LastProcessed += lastProcessedDelta;
                batch.UpdateEntity(bucket, bucket.ETag, TableUpdateMode.Replace);
                await batch.SubmitBatchIfFullAsync();
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        private async Task<List<TimedReprocessBucket>> GetBucketsAsync(TableClientWithRetryContext table)
        {
            var buckets = await GetBucketsWithoutValidationAsync(table);
            if (buckets.Count != BucketedPackage.BucketCount
                || buckets.Any(b => b.Index < 0 || b.Index >= BucketedPackage.BucketCount))
            {
                throw new InvalidOperationException($"The bucket entities are not initialized. You must call {nameof(InitializeAsync)} first.");
            }

            return buckets;
        }

        public async Task<List<TimedReprocessBucket>> GetBucketsAsync()
        {
            var table = await GetTableAsync();
            return await GetBucketsAsync(table);
        }

        private static async Task<List<TimedReprocessBucket>> GetBucketsWithoutValidationAsync(TableClientWithRetryContext table)
        {
            return await table
                .QueryAsync<TimedReprocessBucket>()
                .Where(b => b.PartitionKey == TimedReprocessBucket.DefaultPartitionKey)
                .ToListAsync();
        }

        private async Task<TableClientWithRetryContext> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.TimedReprocessTableName);
        }
    }
}
