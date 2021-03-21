using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public class VersionSetService : IVersionSetProvider
    {
        private readonly NewServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<VersionSetService> _logger;

        public VersionSetService(
            NewServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options,
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
            (var data, _) = await ReadOrNullAsync<CaseInsensitiveDictionary<CaseInsensitiveDictionary<bool>>>();
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
                var data = await MessagePackSerializer.DeserializeAsync<Versions<T>>(info.Content, ExplorePackagesMessagePack.Options);
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

        public async Task UpdateAsync(DateTimeOffset commitTimestamp, SortedDictionary<string, SortedDictionary<string, bool>> idToVersionToDeleted)
        {
            // Read the existing data.
            (var data, var etag) = await ReadOrNullAsync<SortedDictionary<string, SortedDictionary<string, bool>>>();
            BlobRequestConditions requestConditions;
            if (data == null)
            {
                data = new Versions<SortedDictionary<string, SortedDictionary<string, bool>>>
                {
                    V1 = new DataV1<SortedDictionary<string, SortedDictionary<string, bool>>>
                    {
                        CommitTimestamp = commitTimestamp,
                        IdToVersionToDeleted = idToVersionToDeleted
                    }
                };
                requestConditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
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
                var versionsAdded = 0;
                var versionsUpdated = 0;
                var versionsUnchanged = 0;
                foreach (var newId in idToVersionToDeleted)
                {
                    if (!data.V1.IdToVersionToDeleted.TryGetValue(newId.Key, out var oldVersions))
                    {
                        oldVersions = new SortedDictionary<string, bool>();
                        data.V1.IdToVersionToDeleted.Add(newId.Key, oldVersions);
                        idsAdded++;
                    }

                    foreach (var newVersion in newId.Value)
                    {
                        if (!oldVersions.TryGetValue(newVersion.Key, out var oldDeleted))
                        {
                            oldVersions.Add(newVersion.Key, newVersion.Value);
                            versionsAdded++;
                        }
                        else if (oldDeleted != newVersion.Value)
                        {
                            oldVersions[newVersion.Key] = newVersion.Value;
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
                    "versions added: {VersionsAdded}, " +
                    "versions updated: {VersionsUpdated}, " +
                    "versions unchanged: {VersionsUnchanged}.",
                    idsAdded,
                    versionsAdded,
                    versionsUpdated,
                    versionsUnchanged);

                data.V1.CommitTimestamp = commitTimestamp;
                requestConditions = new BlobRequestConditions { IfMatch = etag };
            }

            _logger.LogInformation("Writing the version set to storage with commit timestamp {CommitTimestamp:O}...", commitTimestamp);


            using var stream = await (await GetBlobAsync()).OpenWriteAsync(overwrite: true, new BlockBlobOpenWriteOptions
            {
                OpenConditions = requestConditions,
            });
            await MessagePackSerializer.SerializeAsync(stream, data, ExplorePackagesMessagePack.Options);
            _logger.LogInformation("Done writing the version set to storage.");
        }

        private async Task<BlockBlobClient> GetBlobAsync()
        {
            return (await GetContainerAsync()).GetBlockBlobClient("version-set.dat");
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
