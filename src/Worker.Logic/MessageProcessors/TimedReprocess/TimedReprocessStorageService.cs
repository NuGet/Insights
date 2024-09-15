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
        public const string MetricIdPrefix = $"{nameof(TimedReprocessStorageService)}.";

        private readonly TimeProvider _timeProvider;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<TimedReprocessStorageService> _logger;
        private readonly IMetric _processingDelay;

        public TimedReprocessStorageService(
            TimeProvider timeProvider,
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<TimedReprocessStorageService> logger)
        {
            _timeProvider = timeProvider;
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;

            _processingDelay = _telemetryClient.GetMetric($"{MetricIdPrefix}ProcessingDelayDays");
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
            var timeWindow = GetCurrentTimeWindow(BucketedPackage.BucketCount);

            if (missingBuckets.Any())
            {
                var batch = new MutableTableTransactionalBatch(table);
                foreach (var index in missingBuckets)
                {
                    batch.AddEntity(new TimedReprocessBucket(index)
                    {
                        ScheduledFor = timeWindow.GetScheduledTime(index),
                    });
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
            return await (await GetTableAsync())
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
                .Skip(_options.Value.OldTimedReprocessRunsToKeep)
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

                await batch.SubmitBatchIfNotEmptyAsync();
            }

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
            var bucketsToProcess = await GetStaleBucketsAsync(BucketedPackage.BucketCount);
            return bucketsToProcess.Select(x => x.Index).ToList();
        }

        public async Task<List<TimedReprocessBucket>> GetBucketsToReprocessAsync()
        {
            return await GetStaleBucketsAsync(_options.Value.TimedReprocessMaxBuckets);
        }

        private async Task<List<TimedReprocessBucket>> GetStaleBucketsAsync(int bucketCount)
        {
            var table = await GetTableAsync();
            var buckets = await GetBucketsAsync(table);

            var currentWindow = GetCurrentTimeWindow(bucketCount);

            var bucketsToProcess = new List<TimedReprocessBucket>();
            var sortedBuckets = buckets.OrderBy(x => x.LastProcessed).ThenBy(x => x.Index).ToList();
            foreach (var bucket in sortedBuckets)
            {
                if (bucket.LastProcessed.HasValue
                    && bucket.LastProcessed > currentWindow.WindowStart)
                {
                    continue;
                }

                if (bucket.LastProcessed is null
                    || bucket.ScheduledFor <= currentWindow.Now)
                {
                    bucketsToProcess.Add(bucket);
                }

                if (bucketsToProcess.Count >= currentWindow.MaxBuckets)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "[{Now:O}] {BucketCount}x buckets to process: {Buckets}, using details {Details}",
                currentWindow.Now,
                bucketsToProcess.Count,
                bucketsToProcess.Select(x => x.Index),
                currentWindow);

            return bucketsToProcess;
        }

        public async Task ResetBucketsAsync()
        {
            var table = await GetTableAsync();
            var buckets = await GetBucketsWithoutValidationAsync(table);

            var previousTimeWindow = GetCurrentTimeWindow(BucketedPackage.BucketCount).GetPrevious();
            var batch = new MutableTableTransactionalBatch(table);

            foreach (var bucket in buckets)
            {
                bucket.LastProcessed = null;
                bucket.ScheduledFor = previousTimeWindow.GetScheduledTime(bucket.Index);
                batch.UpdateEntity(bucket, ETag.All, TableUpdateMode.Replace);
                await batch.SubmitBatchIfFullAsync();
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        public async Task MarkBucketsAsProcessedAsync(IEnumerable<int> buckets)
        {
            var indexToBucket = (await GetBucketsAsync()).ToDictionary(x => x.Index);
            var bucketEntities = buckets.Select(x => indexToBucket[x]).ToList();
            var currentTimeWindow = GetCurrentTimeWindow(BucketedPackage.BucketCount);
            var previousTimeWindow = currentTimeWindow.GetPrevious();
            var nextTimeWindow = currentTimeWindow.GetNext();

            var table = await GetTableAsync();
            var batch = new MutableTableTransactionalBatch(table);
            foreach (var bucket in bucketEntities)
            {
                if (bucket.LastProcessed.HasValue)
                {
                    var processDelay = nextTimeWindow.Now - bucket.LastProcessed.Value;
                    _processingDelay.TrackValue(processDelay.TotalDays);
                }

                bucket.LastProcessed = nextTimeWindow.Now;

                var roundedScheduledFor = currentTimeWindow.GetBounding(bucket.ScheduledFor).GetScheduledTime(bucket.Index);
                if (roundedScheduledFor < previousTimeWindow.WindowStart || roundedScheduledFor > nextTimeWindow.WindowEnd)
                {
                    bucket.ScheduledFor = nextTimeWindow.GetScheduledTime(bucket.Index);
                }
                else
                {
                    bucket.ScheduledFor = roundedScheduledFor + currentTimeWindow.Window;
                }

                batch.UpdateEntity(bucket, bucket.ETag, TableUpdateMode.Replace);
                await batch.SubmitBatchIfFullAsync();
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        private static DateTimeOffset RoundDown(DateTimeOffset time, TimeSpan modulus)
        {
            var ticks = time.Ticks;
            return new DateTimeOffset(ticks - (ticks % modulus.Ticks), TimeSpan.Zero);
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

        public TimedReprocessDetails GetCurrentTimeWindow()
        {
            return GetCurrentTimeWindow(_options.Value.TimedReprocessMaxBuckets);
        }

        private TimedReprocessDetails GetCurrentTimeWindow(int maxBuckets)
        {
            return TimedReprocessDetails.Create(_timeProvider.GetUtcNow(), _options.Value.TimedReprocessWindow, maxBuckets);
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
