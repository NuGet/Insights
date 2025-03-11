// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using BenchmarkDotNet.Attributes;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using NuGet.Insights.Worker;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;
using NuGet.Insights.Worker.DownloadsToCsv;

namespace NuGet.Insights.Performance;

/// <summary>
/// Compacts fake PackageDownloads records with the max number of subdivisions.
/// </summary>
public class PackageDownloads_Compact_Fake
{
    [Params(50_000, 100_000, 200_000)]
    public int N = 200_000;

    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
    public AuxiliaryFileUpdaterProcessor<AsOfData<PackageDownloads>, PackageDownloadRecord>? Processor { get; set; }
    public AuxiliaryFileUpdaterMessage<PackageDownloadRecord>? Message { get; set; }
    public TaskState? TaskState { get; set; }
    public NuGetInsightsWorkerSettings? Settings { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        Settings = new NuGetInsightsWorkerSettings { UseMemoryStorage = true };
        var options = Options.Create(Settings);

        var telemetryClient = new TelemetryClient(new TelemetryConfiguration());
        var telemetryClientWrapper = new TelemetryClientWrapper(telemetryClient);

        var serviceClientFactory = new ServiceClientFactory(
            options,
            telemetryClientWrapper,
            LoggerFactory);
        var csvReader = new CsvReaderAdapter(LoggerFactory.CreateLogger<CsvReaderAdapter>());
        var csvRecordStorageService = new CsvRecordStorageService(
            serviceClientFactory,
            csvReader,
            options,
            telemetryClientWrapper,
            LoggerFactory.CreateLogger<CsvRecordStorageService>());

        var versionSetProvider = new AllowAllVersionSetProvider();
        var packageDownloadsClient = new RandomPackageDownloadsClient(seed: 0, N);

        var updater = new DownloadsToCsvUpdater(
            packageDownloadsClient,
            options);

        Processor = new AuxiliaryFileUpdaterProcessor<AsOfData<PackageDownloads>, PackageDownloadRecord>(
            serviceClientFactory,
            csvRecordStorageService,
            versionSetProvider,
            updater,
            options,
            LoggerFactory.CreateLogger<AuxiliaryFileUpdaterProcessor<AsOfData<PackageDownloads>, PackageDownloadRecord>>());
        Message = new AuxiliaryFileUpdaterMessage<PackageDownloadRecord>();
        TaskState = new TaskState();

        await csvRecordStorageService.InitializeAsync(updater.ContainerName);
    }

    [Benchmark]
    public async Task BigMode_MaxDivisions_AllNewRecords()
    {
        Settings!.AppendResultBigModeRecordThreshold = 0; // force big mode
        Settings.AppendResultBigModeSubdivisionSize = 1; // use max subdivisions
        await Processor!.ProcessAsync(Message!, TaskState!, dequeueCount: 0);
    }

    private class RandomPackageDownloadsClient : IPackageDownloadsClient
    {
        private readonly Random _random;
        private readonly int _count;

        public RandomPackageDownloadsClient(int seed, int count)
        {
            _random = new Random(seed);
            _count = count;
        }

        public Task<AsOfData<PackageDownloads>> GetAsync()
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new AsOfData<PackageDownloads>(
                now,
                new Uri("https://example/downloads.v1.json"),
                $"\"{now.Ticks}\"",
                GetRecordsAsync()));
        }

        private async IAsyncEnumerable<IReadOnlyList<PackageDownloads>> GetRecordsAsync()
        {
            await Task.Yield();

            const int pageSize = AsOfData<PackageDownloads>.DefaultPageSize;
            var records = new List<PackageDownloads>(capacity: pageSize);
            var versionsRemaining = 0;
            string id = string.Empty;

            for (var i = 0; i < _count; i++)
            {
                if (versionsRemaining == 0)
                {
                    versionsRemaining = _random.Next(1, 51);
                    id = $"Package{_random.Next()}";
                }

                records.Add(new PackageDownloads(id, $"{versionsRemaining}.0.0", _random.Next(1, 10_000)));
                versionsRemaining--;

                if (records.Count >= pageSize)
                {
                    yield return records;
                    records.Clear();
                }
            }

            if (records.Count > 0)
            {
                yield return records;
            }
        }
    }

    private class AllowAllVersionSetProvider : IVersionSetProvider
    {
        public Task<EntityHandle<IVersionSet>> GetAsync()
        {
            return Task.FromResult(EntityHandle.Create<IVersionSet>(new AllowAllVersionSet()));
        }
    }

    private class AllowAllVersionSet : IVersionSet
    {
        public DateTimeOffset CommitTimestamp => DateTimeOffset.UtcNow;

        public IReadOnlyCollection<string> GetUncheckedIds()
        {
            return [];
        }

        public IReadOnlyCollection<string> GetUncheckedVersions(string id)
        {
            return [];
        }

        public bool TryGetId(string id, out string outId)
        {
            outId = id;
            return true;
        }

        public bool TryGetVersion(string id, string version, out string outVersion)
        {
            outVersion = version;
            return true;
        }
    }
}
