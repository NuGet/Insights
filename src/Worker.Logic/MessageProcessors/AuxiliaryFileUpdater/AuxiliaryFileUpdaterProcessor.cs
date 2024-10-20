// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs.Models;
using NuGet.Insights.Worker.BuildVersionSet;

#nullable enable

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public class AuxiliaryFileUpdaterProcessor<TInput, TRecord> : ITaskStateMessageProcessor<AuxiliaryFileUpdaterMessage<TRecord>>
        where TInput : IAsOfData
        where TRecord : IAuxiliaryFileCsvRecord<TRecord>
    {
        private const string AsOfTimestampMetadata = "asOfTimestamp";
        private const string VersionSetCommitTimestampMetadata = "versionSetCommitTimestamp";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly CsvRecordStorageService _csvRecordStorageService;
        private readonly IVersionSetProvider _versionSetProvider;
        private readonly IAuxiliaryFileUpdater<TInput, TRecord> _updater;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<AuxiliaryFileUpdaterProcessor<TInput, TRecord>> _logger;

        public AuxiliaryFileUpdaterProcessor(
            ServiceClientFactory serviceClientFactory,
            CsvRecordStorageService csvRecordStorageService,
            IVersionSetProvider versionSetProvider,
            IAuxiliaryFileUpdater<TInput, TRecord> updater,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<AuxiliaryFileUpdaterProcessor<TInput, TRecord>> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _csvRecordStorageService = csvRecordStorageService;
            _versionSetProvider = versionSetProvider;
            _updater = updater;
            _options = options;
            _logger = logger;
        }

        public async Task<TaskStateProcessResult> ProcessAsync(AuxiliaryFileUpdaterMessage<TRecord> message, TaskState taskState, long dequeueCount)
        {
            await using var data = await _updater.GetDataAsync();
            using var versionSetHandle = await _versionSetProvider.GetAsync();

            var provider = new AuxiliaryFileCsvRecordProvider(_updater, versionSetHandle.Value, data);
            await _csvRecordStorageService.CompactAsync(provider, _updater.ContainerName, bucket: 0);

            return TaskStateProcessResult.Complete;
        }

        private class AuxiliaryFileCsvRecordProvider : ICsvRecordProvider<TRecord>
        {
            private readonly IAuxiliaryFileUpdater<TInput, TRecord> _updater;
            private readonly IVersionSet _versionSet;
            private TInput? _data;
            private DateTimeOffset _asOfTimestamp;

            public AuxiliaryFileCsvRecordProvider(IAuxiliaryFileUpdater<TInput, TRecord> updater, IVersionSet versionSet, TInput data)
            {
                _updater = updater;
                _versionSet = versionSet;
                _data = data;
                _asOfTimestamp = data.AsOfTimestamp;
            }

            public bool UseExistingRecords => false;
            public bool WriteEmptyCsv => true;

            public bool ShouldCompact(BlobProperties? properties, ILogger logger)
            {
                if (properties != null
                    && properties.Metadata.TryGetValue(AsOfTimestampMetadata, out var unparsedAsOfTimestamp)
                    && DateTimeOffset.TryParse(unparsedAsOfTimestamp, out var latestAsOfTimestamp)
                    && latestAsOfTimestamp == _asOfTimestamp)
                {
                    if (properties.Metadata.TryGetValue(VersionSetCommitTimestampMetadata, out var unparsedVersionSetCommitTimestamp)
                        && DateTimeOffset.TryParse(unparsedVersionSetCommitTimestamp, out var versionSetCommitTimestamp)
                        && versionSetCommitTimestamp == _versionSet.CommitTimestamp)
                    {
                        logger.LogInformation(
                            "The {OperationName} data from {AsOfTimestamp:O} with version set commit timestamp {VersionSetCommitTimestamp:O} already exists.",
                            _updater.OperationName,
                            _asOfTimestamp,
                            versionSetCommitTimestamp);
                        return false;
                    }
                }

                return true;
            }

            public void AddBlobMetadata(Dictionary<string, string> metadata)
            {
                metadata[VersionSetCommitTimestampMetadata] = _versionSet.CommitTimestamp.ToString("O");
                metadata[AsOfTimestampMetadata] = _asOfTimestamp.ToString("O");
            }

            public Task<int> CountRemainingChunksAsync(int bucket, string? lastPosition)
            {
                return Task.FromResult(0);
            }

            public async IAsyncEnumerable<ICsvRecordChunk<TRecord>> GetChunksAsync(int bucket)
            {
                TInput? data;
                if (_data is null)
                {
                    data = await _updater.GetDataAsync();
                    _asOfTimestamp = data.AsOfTimestamp;
                }
                else
                {
                    data = _data;
                    _data = default;
                }

                await foreach (var record in _updater.ProduceRecordsAsync(_versionSet, data))
                {
                    yield return new SingleCsvRecordChunk<TRecord>(record);
                }
            }

            public List<TRecord> Prune(List<TRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
            {
                return records
                    .Distinct()
                    .Order()
                    .ToList();
            }
        }

        private class SingleCsvRecordChunk<T> : ICsvRecordChunk<T> where T : ICsvRecord<T>
        {
            private readonly T _record;

            public SingleCsvRecordChunk(T record)
            {
                _record = record;
            }

            public IReadOnlyList<T> GetRecords() => [_record];
            public string Position => string.Empty;
        }
    }
}
