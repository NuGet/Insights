// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs.Models;
using BenchmarkDotNet.Attributes;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using NuGet.Insights;
using NuGet.Insights.Worker;
using NuGet.Insights.Worker.PackageFileToCsv;

public class CsvRecordStorageService_PackageFiles
{
    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
    public CsvRecordStorageService? Service { get; set; }
    private FakeCsvRecordProvider<PackageFileRecord>? Provider { get; set; }
    public string? ContainerName { get; set; }
    public int Bucket { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var settings = new NuGetInsightsWorkerSettings
        {
            UseMemoryStorage = true,
            AppendResultBigModeRecordThreshold = 0,
            AppendResultBigModeSubdivisionSize = 1,
        };
        ContainerName = settings.PackageFileContainerName;
        Bucket = 0;
        var options = Options.Create(settings);

        var telemetryClient = new TelemetryClient(new TelemetryConfiguration());
        var telemetryClientWrapper = new TelemetryClientWrapper(telemetryClient);

        var serviceClientFactory = new ServiceClientFactory(
            options,
            telemetryClientWrapper,
            LoggerFactory);
        var csvReader = new CsvReaderAdapter(LoggerFactory.CreateLogger<CsvReaderAdapter>());
        Service = new CsvRecordStorageService(
            serviceClientFactory,
            csvReader,
            options,
            telemetryClientWrapper,
            LoggerFactory.CreateLogger<CsvRecordStorageService>());
        Provider = new FakeCsvRecordProvider<PackageFileRecord>(
            csvReader,
            bucket => File.OpenRead($@"C:\Users\joelv\Downloads\packagefiles\compact_{bucket}.csv"));

        await Service.InitializeAsync(ContainerName);
        await Service!.CompactAsync(Provider!, ContainerName!, Bucket);
        Provider.RecordLimit = 0;
    }

    [Benchmark]
    public async Task Baseline()
    {
        await Service!.CompactAsync(Provider!, ContainerName!, Bucket);
    }

    private class FakeCsvRecordProvider<T> : ICsvRecordProvider<T> where T : IAggregatedCsvRecord<T>
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
