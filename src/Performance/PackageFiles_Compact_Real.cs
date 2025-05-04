// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Compression;
using Azure.Storage.Blobs.Models;
using BenchmarkDotNet.Attributes;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using NuGet.Insights.Worker;
using NuGet.Insights.Worker.PackageFileToCsv;

namespace NuGet.Insights.Performance;

/// <summary>
/// Compacts real PackageFile records with the default number of subdivisions.
/// </summary>
public class PackageFiles_Compact_Real
{
    [Params(50_000, 100_000, 200_000)]
    public int N = 200_000;

    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
    public CsvReaderAdapter? CsvReader { get; set; }
    public CsvRecordStorageService? Service { get; set; }
    public FakeCsvRecordProvider<PackageFileRecord>? Provider { get; set; }
    public NuGetInsightsWorkerSettings? Settings { get; set; }
    public string? ContainerName { get; set; }
    public int Bucket { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        Settings = new NuGetInsightsWorkerSettings { UseMemoryStorage = true };
        ContainerName = Settings.PackageFileContainer;
        Bucket = 3;
        var options = Options.Create(Settings);

        var telemetryClient = new TelemetryClient(new TelemetryConfiguration());
        var telemetryClientWrapper = new TelemetryClientWrapper(telemetryClient);

        var serviceClientFactory = new ServiceClientFactory(
            options,
            telemetryClientWrapper,
            LoggerFactory);
        CsvReader = new CsvReaderAdapter(LoggerFactory.CreateLogger<CsvReaderAdapter>());
        var packageFilter = new PackageFilter(telemetryClientWrapper, options);
        Service = new CsvRecordStorageService(
            serviceClientFactory,
            CsvReader,
            packageFilter,
            options,
            telemetryClientWrapper,
            LoggerFactory.CreateLogger<CsvRecordStorageService>());
        Provider = new FakeCsvRecordProvider<PackageFileRecord>(
            CsvReader,
            GetCsvStream);

        await Service.InitializeAsync(ContainerName);
        await Service!.CompactAsync(Provider!, ContainerName!, Bucket);
    }

    [Benchmark]
    public async Task BigMode_DefaultDivisions_NoNewRecords()
    {
        Settings!.AppendResultBigModeRecordThreshold = 0; // force big mode
        Provider!.RecordLimit = 0; // don't introduce new records
        await Service!.CompactAsync(Provider!, ContainerName!, Bucket);
    }

    private Stream GetCsvStream(int bucket)
    {
        var decompressedPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", $"packagefiles_compact_{bucket}.csv");
        var compressedPath = decompressedPath + ".br";
        var logger = LoggerFactory.CreateLogger<PackageFiles_Compact_Real>();
        if (!File.Exists(decompressedPath))
        {
            logger.LogInformation("Decompressing {CompressedPath}...", compressedPath);

            using var compressedStream = File.OpenRead(compressedPath);
            using var decompressedStream = new BrotliStream(compressedStream, CompressionMode.Decompress);
            using var fileStream = new FileStream(decompressedPath, FileMode.Create);
            decompressedStream.CopyTo(fileStream);
        }

        var partialPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", $"packagefiles_compact_{bucket}_{N}.csv");

        if (!File.Exists(partialPath))
        {
            logger.LogInformation("Writing out data file with {Count} records to {PartialPath}...", N, partialPath);

            // write a new file with only the first N lines
            using var readStream = File.OpenRead(decompressedPath);
            using var writeStream = File.Create(partialPath);
            using var streamWriter = new StreamWriter(writeStream);
            using (var streamReader = new StreamReader(readStream))
            {
                PackageFileRecord.WriteHeader(streamWriter);

                var records = CsvReader!
                    .GetRecordsEnumerable<PackageFileRecord>(streamReader, bufferSize: CsvReaderAdapter.MaxBufferSize)
                    .Take(N);
                foreach (var record in records)
                {
                    record.Write(streamWriter);
                }
            }
        }

        return File.OpenRead(partialPath);
    }

    public class FakeCsvRecordProvider<T> : ICsvRecordProvider<T> where T : IAggregatedCsvRecord<T>
    {
        private readonly ICsvReader _csvReader;
        private readonly Func<int, Stream> _getCsvStream;

        public FakeCsvRecordProvider(ICsvReader csvReader, Func<int, Stream> getCsvStream)
        {
            _csvReader = csvReader;
            _getCsvStream = getCsvStream;
        }

        public bool UseExistingRecords => true;
        public bool WriteEmptyCsv => true;

        public int RecordLimit { get; set; } = -1;
        public int RecordPerChunk { get; set; } = 1000;

        public void AddBlobMetadata(Dictionary<string, string> metadata)
        {
        }

        public Task<int> CountRemainingChunksAsync(int bucket, string? lastPosition)
        {
            using var stream = _getCsvStream(bucket);
            using var reader = new StreamReader(stream);
            string? line;
            var count = 0;
            while ((line = reader.ReadLine()) != null)
            {
                count++;
            }

            var totalChunks = (RecordLimit >= 0 ? Math.Min(count, RecordLimit) : count) / RecordPerChunk;
            var chunksCompleted = int.Parse(lastPosition ?? "0", CultureInfo.InvariantCulture);
            return Task.FromResult(totalChunks - chunksCompleted);
        }

        public async IAsyncEnumerable<ICsvRecordChunk<T>> GetChunksAsync(int bucket)
        {
            using var stream = _getCsvStream(bucket);
            using var reader = new StreamReader(stream);
            var records = _csvReader.GetRecordsEnumerable<T>(reader, bufferSize: CsvReaderAdapter.MaxBufferSize);
            var chunk = new List<T>();
            var position = 1;
            var recordCount = 0;
            foreach (var record in records)
            {
                await Task.Yield();

                if (RecordLimit >= 0 && recordCount >= RecordLimit)
                {
                    break;
                }

                chunk.Add(record);
                recordCount++;

                if (chunk.Count >= RecordPerChunk)
                {
                    yield return new FakeCsvRecordChunk<T>(chunk, position: position.ToString(CultureInfo.InvariantCulture));
                    chunk.Clear();
                    position++;
                }
            }

            if (chunk.Count > 0)
            {
                yield return new FakeCsvRecordChunk<T>(chunk, position: position.ToString(CultureInfo.InvariantCulture));
            }
        }

        public List<T> Prune(List<T> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return T.Prune(records, isFinalPrune, options, logger);
        }

        public bool ShouldCompact(BlobProperties? properties, ILogger logger)
        {
            return true;
        }
    }

    private class FakeCsvRecordChunk<T> : ICsvRecordChunk<T> where T : ICsvRecord<T>
    {
        public FakeCsvRecordChunk(IReadOnlyList<T> records, string position)
        {
            _records = records;
            Position = position;
        }

        private readonly IReadOnlyList<T> _records;

        public string Position { get; }
        public IReadOnlyList<T> GetRecords() => _records;
    }
}
