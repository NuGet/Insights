// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class VersionSetService : IVersionSetProvider
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<VersionSetService> _logger;

        public VersionSetService(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<VersionSetService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await (await GetContainerAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task<IVersionSet> GetAsync()
        {
            var versionSet = await GetOrNullAsync();
            if (versionSet == null)
            {
                throw new InvalidOperationException($"No version set is available. Run the {nameof(CatalogScanDriverType.BuildVersionSet)} driver.");
            }

            return versionSet;
        }

        public async Task<IVersionSet> GetOrNullAsync()
        {
            (var data, _) = await ReadOrNullAsync<CaseInsensitiveDictionary<ReadableKey<CaseInsensitiveDictionary<ReadableKey<bool>>>>>();
            if (data == null)
            {
                return null;
            }

            return new VersionSet(data.V1.CommitTimestamp, data.V1.IdToVersionToDeleted);
        }

        private async Task<(Versions<T> data, ETag etag)> ReadOrNullAsync<T>()
        {
            try
            {
                _logger.LogInformation("Reading the version set from storage...");
                var blob = await GetBlobAsync();
                using BlobDownloadInfo info = await blob.DownloadAsync();
                var data = await MessagePackSerializer.DeserializeAsync<Versions<T>>(info.Content, NuGetInsightsMessagePack.Options);
                _logger.LogInformation(
                    "The version set exists with commit timestamp {CommitTimestamp:O} and etag {ETag} and is {Size} bytes.",
                    data.V1.CommitTimestamp,
                    info.Details.ETag,
                    info.ContentLength);
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
            (var data, var etag) = await ReadOrNullAsync<CaseInsensitiveSortedDictionary<ReadableKey<CaseInsensitiveSortedDictionary<ReadableKey<bool>>>>>();

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
                    _logger.LogInformation("The version set in storage is new than the provided data. No update will be done.");
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

                data.V1.CommitTimestamp = commitTimestamp;
                await SaveAsync(
                    commitTimestamp,
                    data,
                    new BlobRequestConditions { IfMatch = etag });
            }
        }

        private async Task SaveAsync<T>(DateTimeOffset commitTimestamp, T data, BlobRequestConditions requestConditions)
        {
            _logger.LogInformation("Writing the version set to the temporary blob with commit timestamp {CommitTimestamp:O}...", commitTimestamp);
            var tempBlob = await GetTempBlobAsync();
            using (var stream = await tempBlob.OpenWriteAsync(overwrite: true))
            {
                await MessagePackSerializer.SerializeAsync(stream, data, NuGetInsightsMessagePack.Options);
            }
            _logger.LogInformation("Done writing the version set to the temporary blob.");

            var tempBlobUrlWithSas = await _serviceClientFactory.GetBlobReadUrlAsync(tempBlob.BlobContainerName, tempBlob.Name);

            _logger.LogInformation("Copying the temp version set to destination blob...");
            var blob = await GetBlobAsync();
            await blob.SyncCopyFromUriAsync(tempBlobUrlWithSas, new BlobCopyFromUriOptions { DestinationConditions = requestConditions });
            _logger.LogInformation("Done copying the temp version set to the destination blob.");

            await tempBlob.DeleteAsync();
        }

        private async Task<BlockBlobClient> GetBlobAsync()
        {
            return (await GetContainerAsync()).GetBlockBlobClient("version-set.dat");
        }

        private async Task<BlockBlobClient> GetTempBlobAsync()
        {
            return (await GetContainerAsync()).GetBlockBlobClient("version-set.dat.temp");
        }

        private async Task<BlobContainerClient> GetContainerAsync()
        {
            return (await _serviceClientFactory.GetBlobServiceClientAsync())
                .GetBlobContainerClient(_options.Value.VersionSetContainerName);
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
