// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

            var buckets = await GetBucketsAsync(table);
            var missingBuckets = Enumerable
                .Range(0, BucketedPackage.BucketCount)
                .Except(buckets.Select(x => x.Index))
                .ToList();
            var created = DateTimeOffset.UtcNow;
            var window = _options.Value.TimedReprocessWindow;

            if (missingBuckets.Any())
            {
                var batch = new MutableTableTransactionalBatch(table);
                foreach (var index in missingBuckets)
                {
                    batch.AddEntity(new Bucket(index) { LastProcessed = created - (2 * window) });
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
            _logger.LogInformation(
                "Updating timed reprocess run {TimedReprocessRunId} with state {State}.",
                run.GetRunId(),
                run.State);

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
            foreach (var scan in oldRunsToDelete)
            {
                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(table);
                }

                batch.DeleteEntity(scan.PartitionKey, scan.RowKey, scan.ETag);
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        public async Task<List<Bucket>> GetBucketsToReprocessAsync()
        {
            var table = await GetTableAsync();
            var buckets = await GetBucketsAsync(table);

            var window = _options.Value.TimedReprocessWindow;
            var maxBuckets = _options.Value.TimedReprocessMaxBuckets;
            var now = DateTimeOffset.UtcNow;

            var windowStart = new DateTimeOffset(now.Ticks - (now.Ticks % window.Ticks), TimeSpan.Zero);
            var windowEnd = windowStart + window;
            var bucketSize = window / BucketedPackage.BucketCount;
            var currentBucket = (int)((now - windowStart) / bucketSize);

            var bucketsToProcess = new List<Bucket>();

            // Add unprocessed or late buckets.
            foreach (var bucket in buckets.OrderBy(x => x.LastProcessed))
            {
                if (bucket.LastProcessed + window < windowStart)
                {
                    bucketsToProcess.Add(bucket);
                }
                else
                {
                    break;
                }

                if (bucketsToProcess.Count >= maxBuckets)
                {
                    break;
                }
            }

            // Add buckets that are up to the current time's bucket.
            if (bucketsToProcess.Count < maxBuckets)
            {
                foreach (var bucket in buckets)
                {
                    if (bucket.LastProcessed + window < windowEnd &&
                        bucket.Index <= currentBucket)
                    {
                        bucketsToProcess.Add(bucket);
                    }

                    if (bucketsToProcess.Count >= maxBuckets)
                    {
                        break;
                    }
                }
            }

            // Stage the last processed timestamp update but don't commit it.
            foreach (var bucket in bucketsToProcess)
            {
                bucket.LastProcessed += window;
            }

            return bucketsToProcess;
        }

        public async Task MarkBucketsAsProcessedAsync(List<Bucket> buckets)
        {
            var table = await GetTableAsync();
            var batch = new MutableTableTransactionalBatch(table);
            foreach (var bucket in buckets)
            {
                batch.UpdateEntity(bucket, bucket.ETag, TableUpdateMode.Replace);
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        public async Task<List<Bucket>> GetBucketsAsync()
        {
            var table = await GetTableAsync();
            return await GetBucketsAsync(table);
        }

        private static async Task<List<Bucket>> GetBucketsAsync(TableClientWithRetryContext table)
        {
            return await table
                .QueryAsync<Bucket>()
                .Where(b => b.PartitionKey == Bucket.DefaultPartitionKey)
                .ToListAsync();
        }

        private async Task<TableClientWithRetryContext> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.TimedReprocessTableName);
        }
    }
}
