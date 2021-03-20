using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public class VersionSetService : IVersionSetProvider
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<VersionSetService> _logger;

        public VersionSetService(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<VersionSetService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await GetBlob().Container.CreateIfNotExistsAsync(retry: true);
        }

        public async Task<IVersionSet> GetAsync()
        {
            (var data, _) = await ReadOrNullAsync<CaseInsensitiveDictionary<CaseInsensitiveDictionary<bool>>>();
            if (data == null)
            {
                return new VersionSet(DateTimeOffset.MinValue, new CaseInsensitiveDictionary<CaseInsensitiveDictionary<bool>>());
            }

            return new VersionSet(data.V1.CommitTimestamp, data.V1.IdToVersionToDeleted);
        }

        private async Task<(Versions<T> data, string etag)> ReadOrNullAsync<T>()
        {
            try
            {
                _logger.LogInformation("Reading the version set from storage...");
                var blob = GetBlob();
                using var stream = await blob.OpenReadAsync();
                var data = await MessagePackSerializer.DeserializeAsync<Versions<T>>(stream, ExplorePackagesMessagePack.Options);
                _logger.LogInformation(
                    "The version set exists with commit timestamp {CommitTimestamp:O} and etag {ETag} and is {Size} bytes.",
                    data.V1.CommitTimestamp,
                    blob.Properties.ETag,
                    stream.Length);
                return (data, blob.Properties.ETag);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                _logger.LogInformation("The version set does not exist in storage.");
                return (null, null);
            }
        }

        public async Task UpdateAsync(DateTimeOffset commitTimestamp, SortedDictionary<string, SortedDictionary<string, bool>> idToVersionToDeleted)
        {
            // Read the existing data.
            (var data, var etag) = await ReadOrNullAsync<SortedDictionary<string, SortedDictionary<string, bool>>>();
            AccessCondition accessCondition;
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
                accessCondition = AccessCondition.GenerateIfNotExistsCondition();
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
                accessCondition = AccessCondition.GenerateIfMatchCondition(etag);
            }

            _logger.LogInformation("Writing the version set to storage with commit timestamp {CommitTimestamp:O}...", commitTimestamp);
            using var stream = await GetBlob().OpenWriteAsync(accessCondition, options: null, operationContext: null);
            await MessagePackSerializer.SerializeAsync(stream, data, ExplorePackagesMessagePack.Options);
            _logger.LogInformation("Done writing the version set to storage.");
        }

        private CloudBlockBlob GetBlob()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.VersionSetContainerName)
                .GetBlockBlobReference("version-set.dat");
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
