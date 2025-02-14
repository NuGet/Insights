// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Compression;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

#nullable enable

namespace NuGet.Insights.Worker
{
    public class CsvRecordStorageService
    {
        public const string MetricIdPrefix = $"{nameof(CsvRecordStorageService)}.";
        private const string ContentType = "text/plain";
        public const string CompactPrefix = "compact_";
        private const int MinSubdivisions = 2;
        private const int MaxSubdivisions = 50;

        private static string TempDir => Path.Combine(Path.GetTempPath(), "NuGet.Insights");
        private static readonly byte[] SubdivideSuffix = [2];

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ICsvReader _csvReader;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<CsvRecordStorageService> _logger;
        private readonly IMetric _compactDurationMs;
        private readonly IMetric _pruneRecordCount;
        private readonly IMetric _pruneRecordDelta;
        private readonly IMetric _finalRecordCount;
        private readonly IMetric _newRecordCount;
        private readonly IMetric _existingRecordCount;
        private readonly IMetric _pruneChunkCount;
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

        public CsvRecordStorageService(
            ServiceClientFactory serviceClientFactory,
            ICsvReader csvReader,
            IOptions<NuGetInsightsWorkerSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<CsvRecordStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _csvReader = csvReader;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;

            _compactDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.DurationMs",
                "DestContainer",
                "RecordType",
                "Bucket");
            _pruneRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.PruneRecordCount",
                "DestContainer",
                "RecordType",
                "Bucket",
                "IsFinalPrune");
            _pruneRecordDelta = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.PruneRecordDelta",
                "DestContainer",
                "RecordType",
                "Bucket",
                "IsFinalPrune");
            _finalRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.FinalRecordCount",
                "DestContainer",
                "RecordType",
                "Bucket");
            _newRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.NewRecordCount",
                "DestContainer",
                "RecordType",
                "Bucket");
            _existingRecordCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.ExistingRecordCount",
                "DestContainer",
                "RecordType",
                "Bucket");
            _pruneChunkCount = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.EntityCount",
                "DestContainer",
                "RecordType",
                "Bucket");
            _compressedSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.CompressedSizeInBytes",
                "DestContainer",
                "RecordType",
                "Bucket");
            _uncompressedSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.UncompressedSizeInBytes",
                "DestContainer",
                "RecordType",
                "Bucket");
            _blobChange = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BlobChange",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModeSplitAppendedDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitAppendedDurationMs",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModeSplitExistingDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitExistingDurationMs",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModeSplitSerializeDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitSerializeDurationMs",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModeSplitFileSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitFileSize",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModeSplitFileSizeDelta = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.SplitFileSizeDelta",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModeMergeSplitDurationMs = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.MergeSplitDurationMs",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModeSwitch = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.Switch",
                "DestContainer",
                "RecordType",
                "Bucket",
                "Reason");
            _bigModeSubdivisions = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.Subdivisions",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModePreallocateOutputFileSize = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.PreallocateOutputFileSize",
                "DestContainer",
                "RecordType",
                "Bucket");
            _bigModeOutputFileSizeDelta = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(CompactAsync)}.BigMode.OutputFileSizeDelta",
                "DestContainer",
                "RecordType",
                "Bucket");
        }

        public async Task InitializeAsync(string destContainer)
        {
            await (await GetContainerAsync(destContainer)).CreateIfNotExistsAsync(retry: true);
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

        public async Task CompactAsync<T>(ICsvRecordProvider<T> provider, string destContainer, int bucket) where T : ICsvRecord<T>
        {
            var recordType = typeof(T).Name;
            var bucketString = bucket.ToString(CultureInfo.InvariantCulture);
            var stopwatch = Stopwatch.StartNew();

            var compactBlob = await GetCompactBlobClientAsync(destContainer, bucket);

            // Step 1: read the existing blob and determine if a no-op should happen
            BlobProperties? blobProperties = null;
            ExistingBlobInfo? existingBlobInfo = null;
            try
            {
                blobProperties = await compactBlob.GetPropertiesAsync();
                existingBlobInfo = new ExistingBlobInfo(
                    blobProperties.ETag,
                    blobProperties.ContentLength,
                    blobProperties.Metadata,
                    blobProperties.ContentHash);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // the blob does not exist
            }

            if (!provider.ShouldCompact(blobProperties, _logger))
            {
                return;
            }

            // Step 2: determine if "big mode" should be used, based on the existing CSV
            if (blobProperties is not null)
            {
                var existingMetadata = new CsvRecordBlob(compactBlob.BlobContainerName, compactBlob.Name, blobProperties);
                if (existingMetadata is not null
                    && existingMetadata.RecordCount.HasValue
                    && existingMetadata.RecordCount.Value > _options.Value.AppendResultBigModeRecordThreshold)
                {
                    var subdivisions = (int)Math.Clamp(
                        Math.Round(1.0 * existingMetadata.RecordCount.Value / _options.Value.AppendResultBigModeSubdivisionSize),
                        min: MinSubdivisions,
                        max: MaxSubdivisions);
                    _logger.LogInformation(
                        "Switching to big mode with {Subdivisions} subdivisions, based on existing record count of {RecordCount}.",
                        subdivisions,
                        existingMetadata.RecordCount);
                    _bigModeSwitch.TrackValue(1, destContainer, recordType, bucketString, "ExistingRecordCount");
                    await CompactBigModeAsync(provider, destContainer, bucket, subdivisions, existingBlobInfo);
                    return;
                }
            }

            // Step 3: load the new records from table storage into memory
            (var loadResult, var records, var subdivsions) = await LoadNewRecordsToMemoryAsync(provider, destContainer, bucket, recordType);

            switch (loadResult)
            {
                case LoadAppendedRecordsToMemoryResult.Loaded:
                    break;
                case LoadAppendedRecordsToMemoryResult.NoData:
                    return;
                case LoadAppendedRecordsToMemoryResult.BigMode:
                    _bigModeSwitch.TrackValue(1, destContainer, recordType, bucketString, "EstimatedRecordCount");
                    await CompactBigModeAsync(provider, destContainer, bucket, subdivsions, existingBlobInfo);
                    return;
                default:
                    throw new NotImplementedException();
            }

            // Step 4: load the existing records from blob storage into memory
            if (blobProperties is not null && provider.UseExistingRecords)
            {
                existingBlobInfo = await LoadExistingRecordsToMemoryAsync(compactBlob, records, destContainer, recordType, bucketString);
            }

            // Step 5: prune records in memory to remove duplicates and sort
            records = Prune(provider, records, destContainer, recordType, bucketString, isFinalPrune: true);
            _finalRecordCount.TrackValue(records.Count, destContainer, recordType, bucketString);

            // Step 6: serialize the records to a new CSV file
            using var stream = SerializeToMemory(records, writeHeader: true, out var uncompressedSize);

            // Step 7: upload the new CSV file to blob storage
            await UploadAsync(
                provider,
                compactBlob,
                existingBlobInfo,
                stream,
                records.Count,
                uncompressedSize,
                destContainer,
                recordType,
                bucketString);

            _compactDurationMs.TrackValue(stopwatch.Elapsed.TotalMilliseconds, destContainer, recordType, bucketString);
        }

        private List<T> Prune<T>(ICsvRecordProvider<T> provider, List<T> records, string destContainer, string recordType, string bucketString, bool isFinalPrune) where T : ICsvRecord<T>
        {
            if (records.Count != 0)
            {
                var initialCount = records.Count;
                _pruneRecordCount.TrackValue(records.Count, destContainer, recordType, bucketString, isFinalPrune ? "true" : "false");
                records = provider.Prune(records, isFinalPrune, _options, _logger);
                _pruneRecordDelta.TrackValue(records.Count - initialCount, destContainer, recordType, bucketString, isFinalPrune ? "true" : "false");
            }

            if (isFinalPrune)
            {
                var recordToDuplicates = new Dictionary<T, List<T>>(records.Count, T.KeyComparer);
                var allDuplicates = new List<List<T>>();
                foreach (var record in records)
                {
                    if (recordToDuplicates.TryGetValue(record, out var duplicates))
                    {
                        duplicates.Add(record);
                        allDuplicates.Add(duplicates);
                    }
                    else
                    {
                        recordToDuplicates.Add(record, [record]);
                    }
                }

                if (allDuplicates.Count > 0)
                {
                    using var errorCsv = new StringWriter();
                    var totalWritten = 0;
                    const int maxWrite = 10;
                    var totalErrorCount = 0;
                    T.WriteHeader(errorCsv);
                    foreach (var duplicates in allDuplicates)
                    {
                        if (totalWritten < maxWrite - 1)
                        {
                            var written = 0;
                            foreach (var record in duplicates)
                            {
                                written++;
                                if (written > 2 && totalWritten >= maxWrite)
                                {
                                    break;
                                }

                                record.Write(errorCsv);
                            }

                            totalWritten += written;
                        }

                        totalErrorCount += duplicates.Count;
                    }

                    throw new InvalidOperationException(
                        $"At least two records had the same key.{Environment.NewLine}" +
                        $"Type: {typeof(T).FullName}{Environment.NewLine}" +
                        $"Key fields: {string.Join(", ", T.KeyFields)}{Environment.NewLine}" +
                        $"Total duplicates: {totalErrorCount}{Environment.NewLine}" +
                        $"Sample of duplicate records (as CSV):{Environment.NewLine}{errorCsv}");
                }
            }

            return records;
        }

        private enum LoadAppendedRecordsToMemoryResult
        {
            Loaded,
            NoData,
            BigMode,
        }

        private async Task<(LoadAppendedRecordsToMemoryResult Result, List<T> Records, int Subdivisions)> LoadNewRecordsToMemoryAsync<T>(
            ICsvRecordProvider<T> provider,
            string destContainer,
            int bucket,
            string recordType) where T : ICsvRecord<T>
        {
            const int PruneEveryNEntity = 500;
            var records = new List<T>();
            var chunkCount = 0;
            var recordCount = 0;
            var result = LoadAppendedRecordsToMemoryResult.Loaded;
            var subdivisions = 0;
            var bucketString = bucket.ToString(CultureInfo.InvariantCulture);

            string? lastPosition = null;
            await foreach (var chunk in provider.GetChunksAsync(bucket))
            {
                chunkCount++;
                lastPosition = chunk.Position;
                var chunkRecords = chunk.GetRecords();
                recordCount += chunkRecords.Count;

                if (recordCount > _options.Value.AppendResultBigModeRecordThreshold)
                {
                    records.Clear();
                    result = LoadAppendedRecordsToMemoryResult.BigMode;
                    break;
                }

                records.AddRange(chunkRecords);

                // Proactively prune to avoid out of memory exceptions.
                if (chunkCount % PruneEveryNEntity == PruneEveryNEntity - 1 && records.Count != 0)
                {
                    records = Prune(provider, records, destContainer, recordType, bucketString, isFinalPrune: false);
                }
            }

            if (result == LoadAppendedRecordsToMemoryResult.BigMode)
            {
                var averageRecordCount = Math.Ceiling(1.0 * recordCount / chunkCount);

                chunkCount += await provider.CountRemainingChunksAsync(bucket, lastPosition);

                double recordCountEstimate = averageRecordCount * chunkCount;
                subdivisions = (int)Math.Clamp(
                    Math.Ceiling(recordCountEstimate / _options.Value.AppendResultBigModeSubdivisionSize),
                    min: MinSubdivisions,
                    max: MaxSubdivisions);
                _logger.LogInformation(
                    "Switching to big mode with {Subdivisions} subdivisions, based on append record count estimate of {RecordCountEstimate}.",
                    subdivisions,
                    recordCountEstimate);
            }
            else if (records.Count == 0)
            {
                // If there are no entities, then there's no new data. We can stop here.
                result = provider.WriteEmptyCsv ? LoadAppendedRecordsToMemoryResult.Loaded : LoadAppendedRecordsToMemoryResult.NoData;
            }

            _newRecordCount.TrackValue(recordCount, destContainer, recordType, bucketString);
            _pruneChunkCount.TrackValue(chunkCount, destContainer, recordType, bucketString);

            return (result, records, subdivisions);
        }

        private async Task<ExistingBlobInfo?> LoadExistingRecordsToMemoryAsync<T>(
            BlockBlobClient compactBlob,
            List<T> records,
            string destContainer,
            string recordType,
            string bucketString) where T : ICsvRecord<T>
        {
            try
            {
                var (existingRecords, existingInfo) = await DeserializeBlobAsync<T>(compactBlob);
                _existingRecordCount.TrackValue(existingRecords.Count, destContainer, recordType, bucketString);
                records.AddRange(existingRecords);
                return existingInfo;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private async Task CompactBigModeAsync<T>(
            ICsvRecordProvider<T> provider,
            string destContainer,
            int bucket,
            int subdivisions,
            ExistingBlobInfo? existingBlobInfo) where T : ICsvRecord<T>
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
                var bucketString = bucket.ToString(CultureInfo.InvariantCulture);

                _bigModeSubdivisions.TrackValue(subdivisions, destContainer, recordType, bucketString);

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
                var shouldNoOp = await LoadNewRecordsToDiskAsync(provider, tempFiles, destContainer, bucket, recordType, subdivisions);
                if (shouldNoOp)
                {
                    return;
                }

                // Step 3: load the existing records from into the temporary files
                var compactBlob = await GetCompactBlobClientAsync(destContainer, bucket);
                if (provider.UseExistingRecords)
                {
                    existingBlobInfo = await LoadExistingRecordsToDiskAsync<T>(
                        tempFiles,
                        destContainer,
                        bucket,
                        subdivisions,
                        recordType,
                        uniqueFileNamePiece,
                        compactBlob);
                }

                // Step 4: prune each temporary file to remove duplicates and sort
                var (pruneRecordCount, totalUncompressedSize) = PruneSubdivisionsOnDisk(provider, tempFiles, destContainer, subdivisions, recordType, bucketString);

                // Step 5: combine the temporary files into one merged, gzipped file
                var finalPath = Path.Combine(TempDir, $"{recordType}_{CompactPrefix}{bucket}_final_{uniqueFileNamePiece}.csv.gz");
                using var finalStream = TempStreamWriter.NewTempFile(finalPath);
                finalStream.SetLengthAndWrite(totalUncompressedSize);
                _bigModePreallocateOutputFileSize.TrackValue(totalUncompressedSize, destContainer, recordType, bucketString);
                var (combineRecordCount, uncompressedSize) = CombineSubdivisionsOnDisk<T>(tempFiles, destContainer, subdivisions, recordType, bucketString, finalStream);

                if (combineRecordCount != pruneRecordCount)
                {
                    throw new InvalidOperationException(
                        $"The number of records written to the final CSV does not match the number of records after pruning. " +
                        $"Expected: {pruneRecordCount}. Actual: {combineRecordCount}.");
                }

                // Step 6: upload the merged file to blob storage
                await UploadAsync(
                    provider,
                    compactBlob,
                    existingBlobInfo,
                    finalStream,
                    combineRecordCount,
                    uncompressedSize,
                    destContainer,
                    recordType,
                    bucketString);

                _compactDurationMs.TrackValue(stopwatch.Elapsed.TotalMilliseconds, destContainer, recordType, bucketString);
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

        private async Task<bool> LoadNewRecordsToDiskAsync<T>(
            ICsvRecordProvider<T> provider,
            List<StreamWriter> tempFiles,
            string destContainer,
            int bucket,
            string recordType,
            int subdivisions) where T : ICsvRecord<T>
        {
            var chunkCount = 0;
            var newRecordCount = 0;
            var bucketString = bucket.ToString(CultureInfo.InvariantCulture);

            await foreach (var chunk in provider.GetChunksAsync(bucket))
            {
                chunkCount++;
                var appendedSw = Stopwatch.StartNew();
                var records = chunk.GetRecords();
                newRecordCount += records.Count;
                DivideAndWriteRecords(subdivisions, tempFiles, records);
                appendedSw.Stop();
                _bigModeSplitAppendedDurationMs.TrackValue(appendedSw.Elapsed.TotalMilliseconds, destContainer, recordType, bucketString);
            }

            _newRecordCount.TrackValue(newRecordCount, destContainer, recordType, bucketString);
            _pruneChunkCount.TrackValue(chunkCount, destContainer, recordType, bucketString);

            if (newRecordCount == 0)
            {
                // If there are no entities, then there's no new data. We can stop here.
                return !provider.WriteEmptyCsv;
            }

            return false;
        }

        private async Task<ExistingBlobInfo?> LoadExistingRecordsToDiskAsync<T>(
            List<StreamWriter> tempFiles,
            string destContainer,
            int bucket,
            int subdivisions,
            string recordType,
            string uniqueFileNamePiece,
            BlockBlobClient compactBlob) where T : ICsvRecord<T>
        {
            ExistingBlobInfo? existingBlobInfo;

            try
            {
                var sw = Stopwatch.StartNew();

                using BlobDownloadStreamingResult blobResult = await compactBlob.DownloadStreamingAsync();
                var isGzip = blobResult.Details.ContentEncoding == "gzip";
                var extension = isGzip ? ".csv.gz" : ".csv";
                var bucketString = bucket.ToString(CultureInfo.InvariantCulture);

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
                    blobResult.Details.ETag,
                    blobResult.Details.ContentLength,
                    blobResult.Details.Metadata,
                    blobResult.Details.ContentHash);

                sw.Stop();

                _existingRecordCount.TrackValue(existingRecordCount, destContainer, recordType, bucketString);
                _bigModeSplitExistingDurationMs.TrackValue(sw.Elapsed.TotalMilliseconds, destContainer, recordType, bucketString);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                existingBlobInfo = null;
            }

            return existingBlobInfo;
        }

        private (long RecordCount, long TotalUncompressedSize) PruneSubdivisionsOnDisk<T>(
            ICsvRecordProvider<T> provider,
            List<StreamWriter> tempFiles,
            string destContainer,
            int subdivisions,
            string recordType,
            string bucketString) where T : ICsvRecord<T>
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

                _bigModeSplitFileSize.TrackValue(tempFile.BaseStream.Length, destContainer, recordType, bucketString);

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
                    records = Prune(provider, records, destContainer, recordType, bucketString, isFinalPrune: true);
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
                    _bigModeSplitFileSizeDelta.TrackValue(fileSizeDelta, destContainer, recordType, bucketString);
                    if (fileSizeDelta < 0)
                    {
                        tempFile.BaseStream.SetLength(countingStream.Length);
                    }
                }

                sw.Stop();
                _bigModeSplitSerializeDurationMs.TrackValue(sw.Elapsed.TotalMilliseconds, destContainer, recordType, bucketString);
            }

            return (recordCount, totalUncompressedSize);
        }

        private (long CombinedRecordCount, long UncompressedSize) CombineSubdivisionsOnDisk<T>(
            List<StreamWriter> tempFiles,
            string destContainer,
            int subdivisions,
            string recordType,
            string bucketString,
            FileStream finalStream) where T : ICsvRecord<T>
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

                _bigModeMergeSplitDurationMs.TrackValue(sw.Elapsed.TotalMilliseconds, destContainer, recordType, bucketString);

                uncompressedSize = uncompressedCountingStream.Length;
                compressedSize = compressedCountingStream.Length;
            }

            _finalRecordCount.TrackValue(finalRecordCountFromWrite, destContainer, recordType, bucketString);

            finalStream.Flush();
            finalStream.Position = 0;
            var delta = compressedSize - finalStream.Length;
            _bigModeOutputFileSizeDelta.TrackValue(delta, destContainer, recordType, bucketString);
            if (delta < 0)
            {
                finalStream.SetLength(compressedSize);
            }

            return (finalRecordCountFromWrite, uncompressedSize);
        }

        private async Task UploadAsync<T>(
            ICsvRecordProvider<T> provider,
            BlockBlobClient compactBlob,
            ExistingBlobInfo? existingBlobInfo,
            Stream stream,
            long recordCount,
            long uncompressedSize,
            string destContainer,
            string recordType,
            string bucketString) where T : ICsvRecord<T>
        {
            BlobRequestConditions requestConditions;
            if (existingBlobInfo is null)
            {
                requestConditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
            }
            else
            {
                requestConditions = new BlobRequestConditions { IfMatch = existingBlobInfo.ETag };
            }

            var metadata = new Dictionary<string, string>
            {
                {
                    StorageUtility.RawSizeBytesMetadata,
                    uncompressedSize.ToString(CultureInfo.InvariantCulture)
                },
                {
                    StorageUtility.RecordCountMetadata,
                    recordCount.ToString(CultureInfo.InvariantCulture)
                },
            };
            provider.AddBlobMetadata(metadata);

            BlobContentInfo uploadInfo = await compactBlob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    Conditions = requestConditions,
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = ContentType,
                        ContentEncoding = "gzip",
                    },
                    Metadata = metadata,
                });

            _compressedSize.TrackValue(stream.Length, destContainer, recordType, bucketString);
            _uncompressedSize.TrackValue(uncompressedSize, destContainer, recordType, bucketString);
            var changed = existingBlobInfo is null || !existingBlobInfo.PreviousHash.SequenceEqual(uploadInfo.ContentHash);
            _blobChange.TrackValue(changed ? 1 : 0, destContainer, recordType, bucketString);
        }

        private record ExistingBlobInfo(ETag ETag, long ContentLength, IDictionary<string, string> Metadata, byte[] PreviousHash);

        private static long DivideAndWriteRecords<T>(
            int subdivisions,
            List<StreamWriter> tempFiles,
            IEnumerable<T> records) where T : ICsvRecord<T>
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

        private async Task<(List<T> Records, ExistingBlobInfo ExistingInfo)> DeserializeBlobAsync<T>(BlockBlobClient blob) where T : ICsvRecord<T>
        {
            var bufferSize = 32 * 1024;
            do
            {
                (var result, var details) = await DeserializeBlobAsync<T>(blob, bufferSize);
                switch (result.Type)
                {
                    case CsvReaderResultType.Success:
                        var info = new ExistingBlobInfo(details.ETag, details.ContentLength, details.Metadata, details.ContentHash);
                        return (result.Records, info);

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
