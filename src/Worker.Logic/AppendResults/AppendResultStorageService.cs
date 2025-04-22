// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Blobs.Models;
using MessagePack;
using NuGet.Insights.WideEntities;

#nullable enable

namespace NuGet.Insights.Worker
{
    public class AppendResultStorageService
    {
        public const string MetricIdPrefix = $"{nameof(AppendResultStorageService)}.";

        private readonly WideEntityService _wideEntityService;
        private readonly CsvRecordStorageService _csvRecordStorageService;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<AppendResultStorageService> _logger;
        private readonly IMetric _appendRecordCount;
        private readonly IMetric _appendSize;
        private readonly IMetric _appendBucketsInBatch;
        private readonly IMetric _tooLargeRecordCount;
        private readonly IMetric _tooLargeSizeInBytes;

        public AppendResultStorageService(
            WideEntityService wideEntityService,
            CsvRecordStorageService csvRecordStorageService,
            ITelemetryClient telemetryClient,
            ILogger<AppendResultStorageService> logger)
        {
            _wideEntityService = wideEntityService;
            _csvRecordStorageService = csvRecordStorageService;
            _telemetryClient = telemetryClient;
            _logger = logger;

            _appendRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.RecordCount",
                "RecordType",
                "Bucket");
            _appendSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.SizeInBytes",
                "RecordType",
                "Bucket");
            _appendBucketsInBatch = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.BucketsInBatch",
                "RecordType");
            _tooLargeRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.TooLarge.RecordCount",
                "RecordType");
            _tooLargeSizeInBytes = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.TooLarge.SizeInBytes",
                "RecordType");
        }

        public async Task InitializeAsync(string srcTable)
        {
            await _wideEntityService.CreateTableAsync(srcTable);
        }

        public async Task DeleteAsync(string srcTable)
        {
            await _wideEntityService.DeleteTableAsync(srcTable);
        }

        public async Task AppendAsync<T>(string srcTable, int bucketCount, IEnumerable<T> records) where T : IAggregatedCsvRecord<T>
        {
            var recordType = typeof(T).Name;
            var bucketGroups = records
                .GroupBy(x => x.GetBucketKey())
                .SelectMany(g => g.Select(x => (Bucket: StorageUtility.GetBucket(bucketCount, g.Key), Record: x)))
                .GroupBy(g => g.Bucket, g => g.Record);
            var bucketsInBatch = 0;
            foreach (var group in bucketGroups)
            {
                var groupRecords = group.ToList();
                foreach (var record in groupRecords)
                {
                    record.SetEmptyStrings();
                }

                await AppendAsync(recordType, srcTable, group.Key, groupRecords);
                bucketsInBatch++;
            }

            _appendBucketsInBatch.TrackValue(bucketsInBatch, recordType);
        }

        private async Task AppendAsync<T>(string recordType, string srcTable, int bucket, IReadOnlyList<T> records) where T : IAggregatedCsvRecord<T>
        {
            // Append the data.
            await AppendToTableAsync(recordType, bucket, srcTable, records);

            // Append a marker to show that this bucket has data.
            try
            {
                await _wideEntityService.InsertAsync(
                   srcTable,
                   partitionKey: string.Empty,
                   rowKey: bucket.ToString(CultureInfo.InvariantCulture),
                   content: Array.Empty<byte>());
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                // This is okay. The marker already exists.
            }
        }

        private async Task AppendToTableAsync<T>(string recordType, int bucket, string srcTable, IReadOnlyList<T> records) where T : ICsvRecord
        {
            var bytes = Serialize(records);
            if (bytes.Length > WideEntityService.MaxTotalDataSize)
            {
                await SplitThenAppendToTableAsync(recordType, bucket, srcTable, records, bytes);
                return;
            }

            var bucketString = bucket.ToString(CultureInfo.InvariantCulture);

            try
            {
                var attempt = 0;
                while (true)
                {
                    attempt++;
                    var rowKey = StorageUtility.GenerateDescendingId().ToString();
                    try
                    {
                        await _wideEntityService.InsertAsync(srcTable, partitionKey: bucket.ToString(CultureInfo.InvariantCulture), rowKey, content: bytes);
                        _appendRecordCount.TrackValue(records.Count, recordType, bucketString);
                        _appendSize.TrackValue(bytes.Length, recordType, bucketString);
                        break;
                    }
                    catch (RequestFailedException ex) when (attempt < 3 && ex.Status == (int)HttpStatusCode.Conflict)
                    {
                        // These conflicts can occur if there is an internal retry on the insert. Just in case, we'll
                        // insert the data again and allow the in-memory pruning to remove duplicates later.
                        _logger.LogTransientWarning(
                            ex,
                            "Conflict when inserting {Count} CSV records into bucket {Bucket} with row key {RowKey}.",
                            records.Count,
                            bucket,
                            rowKey);
                    }
                }
            }
            catch (RequestFailedException ex) when (
                records.Count >= 2
                && ((ex.Status == (int)HttpStatusCode.RequestEntityTooLarge && ex.ErrorCode == "RequestBodyTooLarge")
                    || (ex.Status == (int)HttpStatusCode.BadRequest && ex.ErrorCode == "EntityTooLarge")))
            {
                await SplitThenAppendToTableAsync(recordType, bucket, srcTable, records, bytes);
            }
        }

        private async Task SplitThenAppendToTableAsync<T>(string recordType, int bucket, string srcTable, IReadOnlyList<T> records, byte[] bytes) where T : ICsvRecord
        {
            _tooLargeRecordCount.TrackValue(records.Count, recordType);
            _tooLargeSizeInBytes.TrackValue(bytes.Length, recordType);

            var firstHalf = records.Take(records.Count / 2).ToList();
            var secondHalf = records.Skip(firstHalf.Count).ToList();
            await AppendToTableAsync(recordType, bucket, srcTable, firstHalf);
            await AppendToTableAsync(recordType, bucket, srcTable, secondHalf);
        }

        public async Task CompactAsync<T>(
            string srcTable,
            string destContainer,
            int bucket) where T : IAggregatedCsvRecord<T>
        {
            var adapter = new WideEntityCsvRecordProvider<T>(srcTable, _wideEntityService);
            await _csvRecordStorageService.CompactAsync(adapter, destContainer, bucket);
        }

        public async Task<List<int>> GetAppendedBucketsAsync(string srcTable)
        {
            var markerEntities = await _wideEntityService.RetrieveAsync(srcTable, partitionKey: string.Empty);
            return markerEntities.Select(x => int.Parse(x.RowKey, CultureInfo.InvariantCulture)).ToList();
        }

        private static byte[] Serialize<T>(IEnumerable<T> records) where T : ICsvRecord
        {
            return MessagePackSerializer.Serialize(records, NuGetInsightsMessagePack.Options);
        }

        private static List<T> Deserialize<T>(Stream stream) where T : ICsvRecord
        {
            return MessagePackSerializer.Deserialize<List<T>>(stream, NuGetInsightsMessagePack.Options);
        }

        private class WideEntityCsvRecordProvider<T> : ICsvRecordProvider<T> where T : IAggregatedCsvRecord<T>
        {
            private readonly string _srcTable;
            private readonly WideEntityService _wideEntityService;

            public WideEntityCsvRecordProvider(string srcTable, WideEntityService wideEntityService)
            {
                _srcTable = srcTable;
                _wideEntityService = wideEntityService;
            }

            public bool ShouldCompact(BlobProperties? properties, ILogger logger) => true;

            public bool UseExistingRecords => true;
            public bool WriteEmptyCsv => false;

            public async IAsyncEnumerable<ICsvRecordChunk<T>> GetChunksAsync(int bucket)
            {
                var entities = _wideEntityService.RetrieveAsync(
                    _srcTable,
                    partitionKey: bucket.ToString(CultureInfo.InvariantCulture),
                    minRowKey: null,
                    maxRowKey: null,
                    includeData: true,
                    maxPerPage: StorageUtility.MaxTakeCount);

                await foreach (var entity in entities)
                {
                    yield return new WideEntityChunk<T>(entity);
                }
            }

            public async Task<int> CountRemainingChunksAsync(int bucket, string? lastPosition)
            {
                var additionalEntityCount = await _wideEntityService.RetrieveAsync(
                    _srcTable,
                    partitionKey: bucket.ToString(CultureInfo.InvariantCulture),
                    minRowKey: lastPosition,
                    maxRowKey: new string(char.MaxValue, 1),
                    includeData: false,
                    maxPerPage: StorageUtility.MaxTakeCount).CountAsync();

                return additionalEntityCount - 1; // -1 because the min bound is inclusive
            }

            public List<T> Prune(List<T> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
            {
                return T.Prune(records, isFinalPrune, options, logger);
            }

            public void AddBlobMetadata(Dictionary<string, string> metadata)
            {
            }
        }

        private class WideEntityChunk<T> : ICsvRecordChunk<T> where T : IAggregatedCsvRecord<T>
        {
            private readonly WideEntity _entity;

            public WideEntityChunk(WideEntity entity)
            {
                _entity = entity;
            }

            public string Position => _entity.RowKey;

            public IReadOnlyList<T> GetRecords()
            {
                using var stream = _entity.GetStream();
                return Deserialize<T>(stream);
            }
        }
    }
}
