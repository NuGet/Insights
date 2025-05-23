// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using MessagePack;
using VersionListData = NuGet.Insights.CaseInsensitiveDictionary<NuGet.Insights.ReadableKey<NuGet.Insights.CaseInsensitiveDictionary<NuGet.Insights.ReadableKey<bool>>>>;

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class VersionSetService : IVersionSetProvider
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly EntityReferenceCounter<Task<Versions<VersionListData>>> _cachedVersionSet = new EntityReferenceCounter<Task<Versions<VersionListData>>>();
        private readonly ContainerInitializationState _initializationState;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<VersionSetService> _logger;

        public VersionSetService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<VersionSetService> logger)
        {
            _initializationState = ContainerInitializationState.BlobContainer(serviceClientFactory, options.Value, options.Value.VersionSetContainerName);
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        public async Task DestroyAsync()
        {
            await _initializationState.DestroyAsync();
        }

        public async Task<EntityHandle<IVersionSet>> GetAsync()
        {
            var versionSet = await GetOrNullAsync();
            if (versionSet.Value == null)
            {
                versionSet.Dispose();
                throw new InvalidOperationException($"No version set is available. Run the {nameof(CatalogScanDriverType.BuildVersionSet)} driver.");
            }

            return versionSet;
        }

        public async Task<EntityHandle<IVersionSet>> GetOrNullAsync()
        {
            Task<Versions<VersionListData>> task;

            await _semaphore.WaitAsync();
            try
            {
                task = _cachedVersionSet.Value;

                if (task is null)
                {
                    task = GetOrNullWithoutLockAsync();
                    _cachedVersionSet.Value = task;
                }

                var data = await task;
                var versionSet = data is null ? null : new VersionSet(data.V1.CommitTimestamp, data.V1.IdToVersionToDeleted);

                return new EntityHandle<IVersionSet>(_cachedVersionSet, versionSet);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<Versions<VersionListData>> GetOrNullWithoutLockAsync()
        {
            var sw = Stopwatch.StartNew();
            (var data, _) = await ReadOrNullWithoutLockingAsync<VersionListData>();
            _telemetryClient.TrackMetric(nameof(VersionSetService) + ".ReadUnsorted.ElapsedMs", sw.Elapsed.TotalMilliseconds);
            if (data == null)
            {
                return null;
            }

            return data;
        }

        private async Task<(Versions<T> data, ETag etag)> ReadOrNullWithoutLockingAsync<T>()
        {
            try
            {
                _logger.LogInformation("Reading the version set from storage...");
                var blob = await GetBlobAsync();
                using BlobDownloadStreamingResult info = await blob.DownloadStreamingAsync();
                var data = await MessagePackSerializer.DeserializeAsync<Versions<T>>(info.Content, NuGetInsightsMessagePack.Options);
                _logger.LogInformation(
                    "The version set exists with commit timestamp {CommitTimestamp:O} and etag {ETag} and is {Size} bytes.",
                    data.V1.CommitTimestamp,
                    info.Details.ETag,
                    info.Details.ContentLength);
                return (data, info.Details.ETag);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                _logger.LogInformation("The version set does not exist in storage.");
                return (null, default);
            }
        }

        public async Task UpdateAsync(DateTimeOffset commitTimestamp, CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>> idToVersionToDeleted)
        {
            // Read the existing data.
            var sw = Stopwatch.StartNew();
            (var data, var etag) = await ReadOrNullWithoutLockingAsync<CaseInsensitiveSortedDictionary<ReadableKey<CaseInsensitiveSortedDictionary<ReadableKey<bool>>>>>();
            _telemetryClient.TrackMetric(nameof(VersionSetService) + ".ReadSorted.ElapsedMs", sw.Elapsed.TotalMilliseconds);

            if (data == null)
            {
                await SaveAsync(
                    commitTimestamp,
                    new Versions<CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>>
                    {
                        V1 = new DataV1<CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>>
                        {
                            CommitTimestamp = commitTimestamp,
                            IdToVersionToDeleted = idToVersionToDeleted
                        }
                    },
                    new BlobRequestConditions { IfNoneMatch = ETag.All });
            }
            else
            {
                if (commitTimestamp < data.V1.CommitTimestamp)
                {
                    _logger.LogInformation("The version set in storage is newer than the provided data. No update will be done.");
                    return;
                }

                // Merge the new data into the existing data.
                var idsAdded = 0;
                var idsCaseChanged = 0;
                var versionsAdded = 0;
                var versionsUpdated = 0;
                var versionsUnchanged = 0;
                foreach ((var newId, var versions) in idToVersionToDeleted)
                {
                    if (!data.V1.IdToVersionToDeleted.TryGetValue(newId, out var oldVersions))
                    {
                        oldVersions = ReadableKey.Create(newId, new CaseInsensitiveSortedDictionary<ReadableKey<bool>>());
                        data.V1.IdToVersionToDeleted.Add(newId, oldVersions);
                        idsAdded++;
                    }
                    else if (oldVersions.Key != newId)
                    {
                        oldVersions.Key = newId;
                        idsCaseChanged++;
                    }

                    foreach ((var version, var isDeleted) in versions)
                    {
                        if (!oldVersions.Value.TryGetValue(version, out var oldDeleted))
                        {
                            oldVersions.Value.Add(version, new ReadableKey<bool>(version, isDeleted));
                            versionsAdded++;
                        }
                        else if (oldDeleted.Key != version || oldDeleted.Value != isDeleted)
                        {
                            oldDeleted.Key = version;
                            oldDeleted.Value = isDeleted;
                            versionsUpdated++;
                        }
                        else
                        {
                            versionsUnchanged++;
                        }
                    }
                }

                _logger.LogInformation("The version set has been updated in memory. " +
                    "IDs added: {IdsAdded}, " +
                    "IDs case changed: {IdsCaseChanged}, " +
                    "versions added: {VersionsAdded}, " +
                    "versions updated: {VersionsUpdated}, " +
                    "versions unchanged: {VersionsUnchanged}.",
                    idsAdded,
                    idsCaseChanged,
                    versionsAdded,
                    versionsUpdated,
                    versionsUnchanged);

                sw.Restart();
                data.V1.CommitTimestamp = commitTimestamp;
                await SaveAsync(
                    commitTimestamp,
                    data,
                    new BlobRequestConditions { IfMatch = etag });
            }
        }

        private async Task SaveAsync<T>(DateTimeOffset commitTimestamp, T data, BlobRequestConditions requestConditions)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Writing the version set to the temporary file with commit timestamp {CommitTimestamp:O}...", commitTimestamp);
            var tempPath = Path.Combine(Path.GetTempPath(), $"NuGet.Insights.VersionSet.{Guid.NewGuid().ToByteArray().ToTrimmedBase32()}.dat");
            using var stream = TempStreamWriter.NewTempFile(tempPath);
            await MessagePackSerializer.SerializeAsync(stream, data, NuGetInsightsMessagePack.Options);
            stream.Flush();
            stream.Position = 0;
            _logger.LogInformation("Done writing the version set to the temporary file.");

            _logger.LogInformation("Uploading the temporary file to the destination blob...");
            var blob = await GetBlobAsync();
            await blob.UploadAsync(stream, new BlobUploadOptions { Conditions = requestConditions });
            _logger.LogInformation("Done copying the temporary file to the destination blob.");

            _telemetryClient.TrackMetric(nameof(VersionSetService) + ".Save.ElapsedMs", sw.Elapsed.TotalMilliseconds);
        }

        private async Task<BlockBlobClient> GetBlobAsync()
        {
            var blobServiceClient = await _serviceClientFactory.GetBlobServiceClientAsync(_options.Value);
            var container = blobServiceClient.GetBlobContainerClient(_options.Value.VersionSetContainerName);
            return container.GetBlockBlobClient("version-set.dat");
        }

        [MessagePackObject]
        public class Versions<T>
        {
            [Key(0)]
            public DataV1<T> V1 { get; set; }
        }

        [MessagePackObject]
        public record DataV1<T>
        {
            [Key(0)]
            public DateTimeOffset CommitTimestamp { get; set; }

            [Key(1)]
            public T IdToVersionToDeleted { get; set; }
        }
    }
}
