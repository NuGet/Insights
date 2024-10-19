// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Compression;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using MessagePack;
using NuGet.Insights.WideEntities;

#nullable enable

namespace NuGet.Insights.Worker
{
    public class AppendResultStorageService
    {
        public const string MetricIdPrefix = $"{nameof(AppendResultStorageService)}.";
        private const string ContentType = "text/plain";
        public const string CompactPrefix = "compact_";

        private static string TempDir => Path.Combine(Path.GetTempPath(), "NuGet.Insights");
        private static readonly byte[] SubdivideSuffix = [2];

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly WideEntityService _wideEntityService;
        private readonly ICsvReader _csvReader;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<AppendResultStorageService> _logger;
        private readonly IMetric _appendRecordCount;
        private readonly IMetric _appendSize;
        private readonly IMetric _appendBucketsInBatch;
        private readonly IMetric _tooLargeRecordCount;
        private readonly IMetric _tooLargeSizeInBytes;
        private readonly IMetric _compactDurationMs;
        private readonly IMetric _pruneRecordCount;
        private readonly IMetric _pruneRecordDelta;
        private readonly IMetric _recordCount;
        private readonly IMetric _pruneEntityCount;
        private readonly IMetric _compressedSize;
        private readonly IMetric _uncompressedSize;
        private readonly IMetric _blobChange;
        private readonly IMetric _bigModeMergeSplitDurationMs;
        private readonly IMetric _bigModeSwitch;
        private readonly IMetric _bigModeSubdivisions;
        private readonly IMetric _bigModePreallocateOutputFileSize;
        private readonly IMetric _bigModeOutputFileSizeDelta;
        private readonly IMetric _bigModeSplitFileSizeDelta;
        private readonly IMetric _bigModeSplitFileSize;
        private readonly IMetric _bigModeSplitSerializeDurationMs;
        private readonly IMetric _bigModeSplitExistingDurationMs;
        private readonly IMetric _bigModeSplitAppendedDurationMs;

        public AppendResultStorageService(
            ServiceClientFactory serviceClientFactory,
            WideEntityService wideEntityService,
            ICsvReader csvReader,
            IOptions<NuGetInsightsWorkerSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<AppendResultStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _wideEntityService = wideEntityService;
            _csvReader = csvReader;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;

            _appendRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.RecordCount",
                "RecordType");
            _appendSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.SizeInBytes",
                "RecordType");
            _appendBucketsInBatch = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.BucketsInBatch",
                "RecordType");
            _tooLargeRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.TooLarge.RecordCount",
                "RecordType");
            _tooLargeSizeInBytes = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(AppendToTableAsync)}.TooLarge.SizeInBytes",
                "RecordType");
            _compactDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.DurationMs",
                "DestContainer",
                "RecordType");
            _pruneRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.PruneRecordCount",
                "DestContainer",
                "RecordType",
                "IsFinalPrune");
            _pruneRecordDelta = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.PruneRecordDelta",
                "DestContainer",
                "RecordType",
                "IsFinalPrune");
            _recordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.RecordCount",
                "DestContainer",
                "RecordType");
            _pruneEntityCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.EntityCount",
                "DestContainer",
                "RecordType");
            _compressedSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.CompressedSizeInBytes",
                "DestContainer",
                "RecordType");
            _uncompressedSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.UncompressedSizeInBytes",
                "DestContainer",
                "RecordType");
            _blobChange = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BlobChange",
                "DestContainer",
                "RecordType");
            _bigModeSplitAppendedDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitAppendedDurationMs",
                "DestContainer",
                "RecordType");
            _bigModeSplitExistingDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitExistingDurationMs",
                "DestContainer",
                "RecordType");
            _bigModeSplitSerializeDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitSerializeDurationMs",
                "DestContainer",
                "RecordType");
            _bigModeSplitFileSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitFileSize",
                "DestContainer",
                "RecordType");
            _bigModeSplitFileSizeDelta = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitFileSizeDelta",
                "DestContainer",
                "RecordType");
            _bigModeMergeSplitDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.MergeSplitDurationMs",
                "DestContainer",
                "RecordType");
            _bigModeSwitch = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.Switch",
                "DestContainer",
                "RecordType",
                "Reason");
            _bigModeSubdivisions = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.Subdivisions",
                "DestContainer",
                "RecordType");
            _bigModePreallocateOutputFileSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.PreallocateOutputFileSize",
                "DestContainer",
                "RecordType");
            _bigModeOutputFileSizeDelta = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.OutputFileSizeDelta",
                "DestContainer",
                "RecordType");
        }

        public async Task InitializeAsync(string srcTable, string destContainer)
        {
            await _wideEntityService.CreateTableAsync(srcTable);
            await (await GetContainerAsync(destContainer)).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteAsync(string srcTable)
        {
            await _wideEntityService.DeleteTableAsync(srcTable);
        }

        public async Task<IReadOnlyList<T>> ReadAsync<T>(string destContainer, int bucket) where T : ICsvRecord<T>
        {
            var compactBlob = await GetCompactBlobClientAsync(destContainer, bucket);

            try
            {
                (var records, _) = await DeserializeBlobAsync<T>(compactBlob);
                return records;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return Array.Empty<T>();
            }
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
                        _appendRecordCount.TrackValue(records.Count, recordType);
                        _appendSize.TrackValue(bytes.Length, recordType);
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
            var recordType = typeof(T).Name;
            var stopwatch = Stopwatch.StartNew();

            // Step 1: determine if "big mode" should be used, based on the existing CSV
            var compactBlob = await GetCompactBlobClientAsync(destContainer, bucket);
            var existingBlob = await GetCsvRecordBlobAsync(compactBlob);
            if (existingBlob is not null
                && existingBlob.RecordCount.HasValue
                && existingBlob.RecordCount.Value > _options.Value.AppendResultBigModeRecordThreshold)
            {
                var subdivisions = (int)Math.Max(2, Math.Round(1.0 * existingBlob.RecordCount.Value / _options.Value.AppendResultBigModeSubdivisionSize));
                _logger.LogInformation(
                    "Switching to big mode with {Subdivisions} subdivisions, based on existing record count of {RecordCount}.",
                    subdivisions,
                    existingBlob.RecordCount);
                _bigModeSwitch.TrackValue(1, destContainer, recordType, "ExistingRecordCount");
                await CompactBigModeAsync<T>(srcTable, destContainer, bucket, subdivisions);
                return;
            }

            // Step 2: load the new records from table storage into memory
            (var loadResult, var records, var subdivsions) = await LoadAppendedRecordsToMemoryAsync<T>(srcTable, bucket, destContainer, recordType);
            switch (loadResult)
            {
                case LoadAppendedRecordsToMemoryResult.Loaded:
                    break;
                case LoadAppendedRecordsToMemoryResult.NoData:
                    return;
                case LoadAppendedRecordsToMemoryResult.BigMode:
                    _bigModeSwitch.TrackValue(1, destContainer, recordType, "EstimatedRecordCount");
                    await CompactBigModeAsync<T>(srcTable, destContainer, bucket, subdivsions);
                    return;
                default:
                    throw new NotImplementedException();
            }

            // Step 3: load the existing records from blob storage into memory
            var existingBlobInfo = await LoadExistingRecordsToMemoryAsync(compactBlob, records);

            // Step 4: prune records in memory to remove duplicates and sort
            records = Prune(records, destContainer, recordType, isFinalPrune: true);
            _recordCount.TrackValue(records.Count, destContainer, recordType);

            // Step 5: serialize the records to a new CSV file
            using var stream = SerializeToMemory(records, writeHeader: true, out var uncompressedSize);

            // Step 6: upload the new CSV file to blob storage
            await UploadAsync(
                compactBlob,
                existingBlobInfo,
                stream,
                records.Count,
                uncompressedSize,
                destContainer,
                recordType);

            _compactDurationMs.TrackValue(stopwatch.Elapsed.TotalMilliseconds, destContainer, recordType);
        }

        private async Task<CsvRecordBlob?> GetCsvRecordBlobAsync(BlockBlobClient compactBlob)
        {
            try
            {
                BlobProperties blobProperties = await compactBlob.GetPropertiesAsync();
                return new CsvRecordBlob(
                    compactBlob.BlobContainerName,
                    compactBlob.Name,
                    blobProperties);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private enum LoadAppendedRecordsToMemoryResult
        {
            Loaded,
            NoData,
            BigMode,
        }

        private async Task<(LoadAppendedRecordsToMemoryResult Result, List<T> Records, int Subdivisions)> LoadAppendedRecordsToMemoryAsync<T>(
            string srcTable,
            int bucket,
            string destContainer,
            string recordType) where T : IAggregatedCsvRecord<T>
        {
            const int PruneEveryNEntity = 500;
            var records = new List<T>();
            var entityCount = 0;
            var recordCount = 0;
            var result = LoadAppendedRecordsToMemoryResult.Loaded;
            var subdivisions = 0;

            var entities = _wideEntityService.RetrieveAsync(
                srcTable,
                partitionKey: bucket.ToString(CultureInfo.InvariantCulture),
                minRowKey: null,
                maxRowKey: null,
                includeData: true,
                maxPerPage: StorageUtility.MaxTakeCount);
            string? lastRowKey = null;
            await foreach (var entity in entities)
            {
                entityCount++;
                lastRowKey = entity.RowKey;

                using var stream = entity.GetStream();
                var entityRecords = Deserialize<T>(stream);
                recordCount += entityRecords.Count;

                if (recordCount > _options.Value.AppendResultBigModeRecordThreshold)
                {
                    records.Clear();
                    result = LoadAppendedRecordsToMemoryResult.BigMode;
                    break;
                }

                records.AddRange(entityRecords);

                // Proactively prune to avoid out of memory exceptions.
                if (entityCount % PruneEveryNEntity == PruneEveryNEntity - 1 && records.Count != 0)
                {
                    records = Prune(records, destContainer, recordType, isFinalPrune: false);
                }
            }

            if (result == LoadAppendedRecordsToMemoryResult.BigMode)
            {
                var averageRecordCount = Math.Ceiling(1.0 * recordCount / entityCount);

                var additionalEntityCount = await _wideEntityService.RetrieveAsync(
                    srcTable,
                    partitionKey: bucket.ToString(CultureInfo.InvariantCulture),
                    minRowKey: lastRowKey,
                    maxRowKey: new string(char.MaxValue, 1),
                    includeData: false,
                    maxPerPage: StorageUtility.MaxTakeCount).CountAsync();
                entityCount += additionalEntityCount - 1; // -1 because the min bound is inclusive

                double recordCountEstimate = averageRecordCount * entityCount;
                subdivisions = (int)Math.Max(2, Math.Ceiling(recordCountEstimate / _options.Value.AppendResultBigModeSubdivisionSize));
                _logger.LogInformation(
                    "Switching to big mode with {Subdivisions} subdivisions, based on append record count estimate of {RecordCountEstimate}.",
                    subdivisions,
                    recordCountEstimate);
            }
            else if (records.Count == 0)
            {
                // If there are no entities, then there's no new data. We can stop here.
                result = LoadAppendedRecordsToMemoryResult.NoData;
            }

            _pruneEntityCount.TrackValue(entityCount, destContainer, recordType);
            return (result, records, subdivisions);
        }

        private async Task<ExistingBlobInfo> LoadExistingRecordsToMemoryAsync<T>(BlockBlobClient compactBlob, List<T> records) where T : IAggregatedCsvRecord<T>
        {
            try
            {
                var (existingRecords, previousDetails) = await DeserializeBlobAsync<T>(compactBlob);
                records.AddRange(existingRecords);
                return new ExistingBlobInfo(
                    new BlobRequestConditions { IfMatch = previousDetails.ETag },
                    previousDetails.ContentHash);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return new ExistingBlobInfo(
                    new BlobRequestConditions { IfNoneMatch = ETag.All },
                    PreviousHash: null);
            }
        }

        private async Task CompactBigModeAsync<T>(
            string srcTable,
            string destContainer,
            int bucket,
            int subdivisions) where T : IAggregatedCsvRecord<T>
        {
            if (subdivisions < 1)
            {
                throw new ArgumentException("The number of subdivisions must be at least 2.", nameof(subdivisions));
            }


            var tempFiles = new List<StreamWriter>();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var recordType = typeof(T).Name;

                _bigModeSubdivisions.TrackValue(subdivisions, destContainer, recordType);

                // Step 1: initialize the N temporary files split the bucket's records into
                var uniqueFileNamePiece = Guid.NewGuid().ToByteArray().ToTrimmedBase32();
                Directory.CreateDirectory(TempDir);
                for (var i = 0; i < subdivisions; i++)
                {
                    var tempPath = Path.Combine(TempDir, $"{recordType}_{CompactPrefix}{bucket}_{i}_{uniqueFileNamePiece}.csv");
                    var fileStream = TempStreamWriter.NewTempFile(tempPath);
                    tempFiles.Add(GetStreamWriter<T>(fileStream, writeHeader: true));
                }

                // Step 2: load the new records from table storage into the temporary files
                var shouldNoOp = await LoadAppendedRecordsToDiskAsync<T>(tempFiles, srcTable, bucket, destContainer, recordType, subdivisions);
                if (shouldNoOp)
                {
                    return;
                }

                // Step 3: load the existing records from into the temporary files
                var compactBlob = await GetCompactBlobClientAsync(destContainer, bucket);
                var existingBlobInfo = await LoadExistingRecordsToDiskAsync<T>(tempFiles, destContainer, bucket, subdivisions, recordType, uniqueFileNamePiece, compactBlob);

                // Step 4: prune each temporary file to remove duplicates and sort
                var (pruneRecordCount, totalUncompressedSize) = PruneSubdivisionsOnDisk<T>(tempFiles, destContainer, subdivisions, recordType);

                // Step 5: combine the temporary files into one merged, gzipped file
                var finalPath = Path.Combine(TempDir, $"{recordType}_{CompactPrefix}{bucket}_final_{uniqueFileNamePiece}.csv.gz");
                using var finalStream = TempStreamWriter.NewTempFile(finalPath);
                finalStream.SetLengthAndWrite(totalUncompressedSize);
                _bigModePreallocateOutputFileSize.TrackValue(totalUncompressedSize, destContainer, recordType);
                var (combineRecordCount, uncompressedSize) = CombineSubdivisionsOnDisk<T>(tempFiles, destContainer, subdivisions, recordType, finalStream);

                if (combineRecordCount != pruneRecordCount)
                {
                    throw new InvalidOperationException(
                        $"The number of records written to the final CSV does not match the number of records after pruning. " +
                        $"Expected: {pruneRecordCount}. Actual: {combineRecordCount}.");
                }

                // Step 6: upload the merged file to blob storage
                await UploadAsync(
                    compactBlob,
                    existingBlobInfo,
                    finalStream,
                    combineRecordCount,
                    uncompressedSize,
                    destContainer,
                    recordType);

                _compactDurationMs.TrackValue(stopwatch.Elapsed.TotalMilliseconds, destContainer, recordType);
            }
            finally
            {
                foreach (var tempFile in tempFiles)
                {
                    string filePath = ((FileStream)tempFile.BaseStream).Name;
                    try
                    {
                        tempFile.BaseStream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // best effort
                        _logger.LogWarning(ex, "Failed to delete temporary CSV file: {FilePath}", filePath);
                    }
                }
            }
        }

        private async Task<bool> LoadAppendedRecordsToDiskAsync<T>(
            List<StreamWriter> tempFiles,
            string srcTable,
            int bucket,
            string destContainer,
            string recordType,
            int subdivisions) where T : IAggregatedCsvRecord<T>
        {
            var entityCount = 0;
            var appendRecordCount = 0;

            var entities = _wideEntityService.RetrieveAsync(
                srcTable,
                partitionKey: bucket.ToString(CultureInfo.InvariantCulture),
                minRowKey: null,
                maxRowKey: null,
                includeData: true,
                maxPerPage: StorageUtility.MaxTakeCount);

            await foreach (var entity in entities)
            {
                entityCount++;
                var appendedSw = Stopwatch.StartNew();
                using var stream = entity.GetStream();
                var records = Deserialize<T>(stream);
                appendRecordCount += records.Count;
                DivideAndWriteRecords(subdivisions, tempFiles, records);
                appendedSw.Stop();
                _bigModeSplitAppendedDurationMs.TrackValue(appendedSw.Elapsed.TotalMilliseconds, destContainer, recordType);
            }

            if (appendRecordCount == 0)
            {
                // If there are no entities, then there's no new data. We can stop here.
                _pruneEntityCount.TrackValue(entityCount, destContainer, recordType);
                return true;
            }

            _pruneEntityCount.TrackValue(entityCount, destContainer, recordType);
            return false;
        }

        private async Task<ExistingBlobInfo> LoadExistingRecordsToDiskAsync<T>(
            List<StreamWriter> tempFiles,
            string destContainer,
            int bucket,
            int subdivisions,
            string recordType,
            string uniqueFileNamePiece,
            BlockBlobClient compactBlob) where T : IAggregatedCsvRecord<T>
        {
            ExistingBlobInfo existingBlobInfo;

            try
            {
                var sw = Stopwatch.StartNew();

                using BlobDownloadStreamingResult blobResult = await compactBlob.DownloadStreamingAsync();
                var isGzip = blobResult.Details.ContentEncoding == "gzip";
                var extension = isGzip ? ".csv.gz" : ".csv";

                var existingPath = Path.Combine(TempDir, $"{recordType}_{CompactPrefix}{bucket}_existing_{uniqueFileNamePiece}{extension}");
                using var existingStream = TempStreamWriter.NewTempFile(existingPath);
                using var hasher = IncrementalHash.CreateNone();
                await existingStream.SetLengthAndWriteAsync(blobResult.Details.ContentLength);
                await blobResult.Content.CopyToSlowAsync(
                    existingStream,
                    blobResult.Details.ContentLength,
                    TempStreamDirectory.DefaultBufferSize,
                    hasher,
                    _logger);

                existingStream.Flush();
                existingStream.Position = 0;

                Stream readStream = existingStream;
                if (isGzip)
                {
                    readStream = new GZipStream(existingStream, CompressionMode.Decompress);
                }

                _logger.LogInformation(
                    "Splitting existing records ({Bytes} bytes) into {Subdivisions} CSV parts for record type {RecordType}.",
                    blobResult.Details.ContentLength,
                    subdivisions,
                    recordType);

                using var existingReader = new StreamReader(readStream);
                var records = _csvReader.GetRecordsEnumerable<T>(existingReader, CsvReaderAdapter.MaxBufferSize);
                var existingRecordCount = DivideAndWriteRecords(subdivisions, tempFiles, records);

                existingBlobInfo = new ExistingBlobInfo(
                    new BlobRequestConditions { IfMatch = blobResult.Details.ETag },
                    blobResult.Details.ContentHash);

                sw.Stop();

                _bigModeSplitExistingDurationMs.TrackValue(sw.Elapsed.TotalMilliseconds, destContainer, recordType);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                existingBlobInfo = new ExistingBlobInfo(
                    new BlobRequestConditions { IfNoneMatch = ETag.All },
                    PreviousHash: null);
            }

            return existingBlobInfo;
        }

        private (long RecordCount, long TotalUncompressedSize) PruneSubdivisionsOnDisk<T>(
            List<StreamWriter> tempFiles,
            string destContainer,
            int subdivisions,
            string recordType) where T : IAggregatedCsvRecord<T>
        {
            long recordCount = 0;
            long totalUncompressedSize = 0;

            for (var i = 0; i < tempFiles.Count; i++)
            {
                var tempFile = tempFiles[i];

                // close the previous writer
                tempFile.Flush();
                tempFile.Dispose();
                tempFile.BaseStream.Flush();
                tempFile.BaseStream.Position = 0;

                _bigModeSplitFileSize.TrackValue(tempFile.BaseStream.Length, destContainer, recordType);

                _logger.LogInformation(
                    "Pruning part {SubdivisionNumber}/{Subdivisions} ({UncompressedBytes} uncompressed bytes) of CSV type {RecordType}.",
                    i + 1,
                    subdivisions,
                    tempFile.BaseStream.Length,
                    recordType);

                // read all of the records and prune
                var sw = Stopwatch.StartNew();
                List<T> records;
                using (var reader = new StreamReader(tempFile.BaseStream, leaveOpen: true))
                {
                    records = _csvReader.GetRecords<T>(reader, CsvReaderAdapter.MaxBufferSize).Records;
                    var initialCount = records.Count;
                    records = Prune(records, destContainer, recordType, isFinalPrune: true);
                    recordCount += records.Count;
                }

                // write the pruned records back to disk
                tempFile.BaseStream.Position = 0;
                using (var countingStream = new CountingWriterStream(tempFile.BaseStream))
                {
                    SerializeRecords(records, countingStream, writeHeader: true);
                    tempFile.BaseStream.Flush();
                    tempFile.BaseStream.Position = 0;
                    totalUncompressedSize += countingStream.Length;
                    var fileSizeDelta = countingStream.Length - tempFile.BaseStream.Length;
                    _bigModeSplitFileSizeDelta.TrackValue(fileSizeDelta, destContainer, recordType);
                    if (fileSizeDelta < 0)
                    {
                        tempFile.BaseStream.SetLength(countingStream.Length);
                    }
                }

                sw.Stop();
                _bigModeSplitSerializeDurationMs.TrackValue(sw.Elapsed.TotalMilliseconds, destContainer, recordType);
            }

            return (recordCount, totalUncompressedSize);
        }

        private (long CombinedRecordCount, long UncompressedSize) CombineSubdivisionsOnDisk<T>(
            List<StreamWriter> tempFiles,
            string destContainer,
            int subdivisions,
            string recordType,
            FileStream finalStream) where T : IAggregatedCsvRecord<T>
        {
            _logger.LogInformation(
                "Merging {Subdivisions} parts into the final CSV for record type {RecordType}.",
                subdivisions,
                recordType);

            long uncompressedSize;
            long compressedSize;
            long finalRecordCountFromWrite = 0;

            using (var compressedCountingStream = new CountingWriterStream(finalStream))
            using (var gzipStream = new GZipStream(compressedCountingStream, CompressionLevel.Optimal, leaveOpen: true))
            using (var uncompressedCountingStream = new CountingWriterStream(gzipStream))
            {
                var sw = Stopwatch.StartNew();

                // open all of the readers
                var readers = tempFiles
                    .Select(x => _csvReader.GetRecordsEnumerable<T>(new StreamReader(x.BaseStream), CsvReaderAdapter.MaxBufferSize))
                    .ToList();

                // open the file writer
                using (var writer = GetStreamWriter<T>(uncompressedCountingStream, writeHeader: true))
                {
                    // merge the records
                    var merged = readers.MergedSorted(x => x);
                    foreach (var record in merged)
                    {
                        record.Write(writer);
                        finalRecordCountFromWrite++;
                    }
                }

                // forcefully dispose the gzip stream so the trailer bytes are written
                gzipStream.Dispose();

                sw.Stop();

                _bigModeMergeSplitDurationMs.TrackValue(sw.Elapsed.TotalMilliseconds, destContainer, recordType);

                uncompressedSize = uncompressedCountingStream.Length;
                compressedSize = compressedCountingStream.Length;
            }

            _recordCount.TrackValue(finalRecordCountFromWrite, destContainer, recordType);

            finalStream.Flush();
            finalStream.Position = 0;
            var delta = compressedSize - finalStream.Length;
            _bigModeOutputFileSizeDelta.TrackValue(delta, destContainer, recordType);
            if (delta < 0)
            {
                finalStream.SetLength(compressedSize);
            }

            return (finalRecordCountFromWrite, uncompressedSize);
        }

        private async Task UploadAsync(
            BlockBlobClient compactBlob,
            ExistingBlobInfo existingBlobInfo,
            Stream stream,
            long recordCount,
            long uncompressedSize,
            string destContainer,
            string recordType)
        {
            BlobContentInfo uploadInfo = await compactBlob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    Conditions = existingBlobInfo.RequestConditions,
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = ContentType,
                        ContentEncoding = "gzip",
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        {
                            StorageUtility.RawSizeBytesMetadata,
                            uncompressedSize.ToString(CultureInfo.InvariantCulture)
                        },
                        {
                            StorageUtility.RecordCountMetadata,
                            recordCount.ToString(CultureInfo.InvariantCulture)
                        },
                    },
                });

            _compressedSize.TrackValue(stream.Length, destContainer, recordType);
            _uncompressedSize.TrackValue(uncompressedSize, destContainer, recordType);
            var changed = existingBlobInfo.PreviousHash is null || !existingBlobInfo.PreviousHash.SequenceEqual(uploadInfo.ContentHash);
            _blobChange.TrackValue(changed ? 1 : 0, destContainer, recordType);
        }

        private record ExistingBlobInfo(
            BlobRequestConditions RequestConditions,
            byte[]? PreviousHash);

        private List<T> Prune<T>(List<T> records, string destContainer, string recordType, bool isFinalPrune) where T : IAggregatedCsvRecord<T>
        {
            if (records.Count != 0)
            {
                var initialCount = records.Count;
                _pruneRecordCount.TrackValue(records.Count, destContainer, recordType, isFinalPrune ? "true" : "false");
                records = T.Prune(records, isFinalPrune, _options, _logger);
                _pruneRecordDelta.TrackValue(records.Count - initialCount, destContainer, recordType, isFinalPrune ? "true" : "false");
            }

            if (isFinalPrune)
            {
                var unique = new HashSet<T>(records.Count, T.KeyComparer);
                foreach (var record in records)
                {
                    if (!unique.Add(record))
                    {
                        using var errorCsv = new StringWriter();
                        T.WriteHeader(errorCsv);
                        record.Write(errorCsv);
                        throw new InvalidOperationException(
                            $"At least two records had the same key.{Environment.NewLine}" +
                            $"Type: {typeof(T).FullName}{Environment.NewLine}" +
                            $"Duplicate record (as CSV):{Environment.NewLine}{errorCsv}");
                    }
                }
            }

            return records;
        }

        private static long DivideAndWriteRecords<T>(
            int subdivisions,
            List<StreamWriter> tempFiles,
            IEnumerable<T> records) where T : IAggregatedCsvRecord<T>
        {
            string? lastBucketKey = null;
            int lastBucket = -1;
            long recordCount = 0;
            foreach (var record in records)
            {
                var bucketKey = record.GetBucketKey();
                if (bucketKey != lastBucketKey)
                {
                    lastBucketKey = bucketKey;

                    // Get a new bucket key, by appending a constant to the old bucket key.
                    // This will further subdive the bucket we are processing.
                    lastBucket = StorageUtility.GetBucket(subdivisions, bucketKey, SubdivideSuffix);
                }

                recordCount++;
                record.Write(tempFiles[lastBucket]);
            }

            return recordCount;
        }

        private async Task<(List<T> records, BlobDownloadDetails details)> DeserializeBlobAsync<T>(BlockBlobClient blob) where T : ICsvRecord<T>
        {
            var bufferSize = 32 * 1024;
            do
            {
                (var result, var details) = await DeserializeBlobAsync<T>(blob, bufferSize);
                switch (result.Type)
                {
                    case CsvReaderResultType.Success:
                        return (result.Records, details);

                    case CsvReaderResultType.BufferTooSmall:
                        bufferSize = CsvReaderAdapter.MaxBufferSize;
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
            while (bufferSize <= CsvReaderAdapter.MaxBufferSize);

            throw new InvalidOperationException($"Could not deserialize blob after trying buffers up to {bufferSize} bytes in size.");
        }

        private async Task<(CsvReaderResult<T> result, BlobDownloadDetails details)> DeserializeBlobAsync<T>(BlockBlobClient blob, int bufferSize)
            where T : ICsvRecord<T>
        {
            using BlobDownloadStreamingResult info = await blob.DownloadStreamingAsync();
            var readStream = info.Content;
            try
            {
                if (info.Details.ContentEncoding == "gzip")
                {
                    readStream = new GZipStream(readStream, CompressionMode.Decompress);
                }

                using var reader = new StreamReader(readStream);
                var result = _csvReader.GetRecords<T>(reader, bufferSize);
                return (result, info.Details);
            }
            finally
            {
                readStream?.Dispose();
            }
        }

        public async Task<List<int>> GetAppendedBucketsAsync(string srcTable)
        {
            var markerEntities = await _wideEntityService.RetrieveAsync(srcTable, partitionKey: string.Empty);
            return markerEntities.Select(x => int.Parse(x.RowKey, CultureInfo.InvariantCulture)).ToList();
        }

        public async Task<List<int>> GetCompactedBucketsAsync(string destContainer)
        {
            var container = await GetContainerAsync(destContainer);
            var buckets = new List<int>();

            if (!await container.ExistsAsync())
            {
                return buckets;
            }

            var regex = new Regex(Regex.Escape(CompactPrefix) + @"(\d+)");
            var blobs = container.GetBlobsAsync(prefix: CompactPrefix);
            await foreach (var blob in blobs)
            {
                var bucket = int.Parse(regex.Match(blob.Name).Groups[1].Value, CultureInfo.InvariantCulture);
                buckets.Add(bucket);
            }

            return buckets;
        }

        public async Task<Uri> GetCompactedBlobUrlAsync(string destContainer, int bucket)
        {
            var blob = await GetCompactBlobClientAsync(destContainer, bucket);
            return blob.Uri;
        }

        public async Task<BlockBlobClient> GetCompactBlobClientAsync(string destContainer, int bucket)
        {
            return (await GetContainerAsync(destContainer)).GetBlockBlobClient($"{CompactPrefix}{bucket}.csv.gz");
        }

        private static byte[] Serialize<T>(IEnumerable<T> records) where T : ICsvRecord
        {
            return MessagePackSerializer.Serialize(records, NuGetInsightsMessagePack.Options);
        }

        private static List<T> Deserialize<T>(Stream stream) where T : ICsvRecord
        {
            return MessagePackSerializer.Deserialize<List<T>>(stream, NuGetInsightsMessagePack.Options);
        }

        private MemoryStream SerializeToMemory<T>(IEnumerable<T> records, bool writeHeader, out long uncompressedSize) where T : ICsvRecord<T>
        {
            var memoryStream = new MemoryStream();
            using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            using (var countingStream = new CountingWriterStream(gzipStream))
            {
                SerializeRecords(records, countingStream, writeHeader);
                uncompressedSize = countingStream.Length;
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        private void SerializeRecords<T>(IEnumerable<T> records, Stream destination, bool writeHeader) where T : ICsvRecord<T>
        {
            using StreamWriter streamWriter = GetStreamWriter<T>(destination, writeHeader);
            SerializeRecords(records, streamWriter);
        }

        private StreamWriter GetStreamWriter<T>(Stream destination, bool writeHeader) where T : ICsvRecord<T>
        {
            var streamWriter = new StreamWriter(destination, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true)
            {
                NewLine = "\n",
            };

            if (writeHeader)
            {
                streamWriter.WriteLine(_csvReader.GetHeader<T>());
            }

            return streamWriter;
        }

        private static void SerializeRecords<T>(IEnumerable<T> records, TextWriter streamWriter) where T : ICsvRecord<T>
        {
            foreach (var record in records)
            {
                record.Write(streamWriter);
            }
        }

        private async Task<BlobContainerClient> GetContainerAsync(string name)
        {
            var serviceClient = await _serviceClientFactory.GetBlobServiceClientAsync();
            return serviceClient.GetBlobContainerClient(name);
        }
    }
}
