using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.WideEntities;
using MessagePack;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    public class PackageWideEntityService
    {
        private readonly WideEntityService _wideEntityService;

        public PackageWideEntityService(
            WideEntityService wideEntityService)
        {
            _wideEntityService = wideEntityService;
        }

        public async Task InitializeAsync(string tableName)
        {
            await _wideEntityService.CreateTableAsync(tableName);
        }

        public async Task<IReadOnlyDictionary<CatalogLeafItem, TOutput>> UpdateBatchAsync<TData, TOutput>(
            string tableName,
            string id,
            IReadOnlyCollection<CatalogLeafItem> leafItems,
            Func<CatalogLeafItem, Task<TOutput>> fetchOutputAsync,
            Func<TOutput, TData> outputToData,
            Func<TData, TOutput> dataToOutput)
            where TData : IPackageWideEntity
        {
            var rowKeyToLeafItem = new Dictionary<string, CatalogLeafItem>();
            foreach (var leafItem in leafItems)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(id, leafItem.PackageId))
                {
                    throw new ArgumentException("All leaf items must have the same package ID.");
                }

                var rowKey = GetRowKey(leafItem.PackageVersion);
                if (rowKeyToLeafItem.ContainsKey(rowKey))
                {
                    throw new ArgumentException("The leaf items must be unique by package version.");
                }

                rowKeyToLeafItem.Add(rowKey, leafItem);
            }

            var partitionKey = GetPartitionKey(id);

            // Fetch get the latest data for all leaf items, where applicable. There are three possibilities for each
            // row keys:
            //   1. The row key does not exist. This means we must fetch the info and insert it into the table.
            //   2. The row exists but the data is stale. This means we must fetch the info and replace it in the table.
            //   3. The row exists and is not stale. We can just return the data in the table.
            var batch = new List<WideEntityOperation>();
            var output = new Dictionary<CatalogLeafItem, TOutput>();
            foreach (var (rowKey, leafItem) in rowKeyToLeafItem)
            {
                (var existingEntity, var matchingData) = await GetExistingAsync<TData>(tableName, partitionKey, rowKey, leafItem);
                if (matchingData == null)
                {
                    var newOutput = await fetchOutputAsync(leafItem);
                    var newBytes = Serialize(outputToData(newOutput));
                    if (existingEntity == null)
                    {
                        batch.Add(WideEntityOperation.Insert(partitionKey, rowKey, newBytes));
                    }
                    else
                    {
                        batch.Add(WideEntityOperation.Replace(existingEntity, newBytes));
                    }

                    output.Add(leafItem, newOutput);
                }
                else
                {
                    output.Add(leafItem, dataToOutput(matchingData));
                }
            }

            await _wideEntityService.ExecuteBatchAsync(tableName, batch, allowBatchSplits: true);

            return output;
        }

        public async Task<TOutput> GetOrUpdateInfoAsync<TData, TOutput>(
            string tableName,
            CatalogLeafItem leafItem,
            Func<CatalogLeafItem, Task<TOutput>> fetchOutputAsync,
            Func<TOutput, TData> outputToData,
            Func<TData, TOutput> dataToOutput)
            where TData : IPackageWideEntity
        {
            var partitionKey = GetPartitionKey(leafItem.PackageId);
            var rowKey = GetRowKey(leafItem.PackageVersion);

            (var existingEntity, var matchingData) = await GetExistingAsync<TData>(tableName, partitionKey, rowKey, leafItem);
            if (matchingData != null)
            {
                return dataToOutput(matchingData);
            }

            var newOutput = await fetchOutputAsync(leafItem);
            var newBytes = Serialize(outputToData(newOutput));

            if (existingEntity != null)
            {
                await _wideEntityService.ReplaceAsync(
                    tableName,
                    existingEntity,
                    newBytes);
            }
            else
            {
                await _wideEntityService.InsertAsync(
                    tableName,
                    partitionKey,
                    rowKey,
                    newBytes);
            }

            return newOutput;
        }

        private async Task<(WideEntity ExistingEntity, T MatchingInfo)> GetExistingAsync<T>(
            string tableName,
            string partitionKey,
            string rowKey,
            CatalogLeafItem leafItem)
            where T : IPackageWideEntity
        {
            var existingEntity = await _wideEntityService.RetrieveAsync(tableName, partitionKey, rowKey);
            if (existingEntity != null)
            {
                var existingInfo = Deserialize<T>(existingEntity);

                // Prefer the existing entity if not older than the current leaf item
                if (leafItem.CommitTimestamp <= existingInfo.CommitTimestamp)
                {
                    return (existingEntity, existingInfo);
                }

                return (existingEntity, default);
            }

            return (null, default);
        }

        private static string GetPartitionKey(string id)
        {
            return id.ToLowerInvariant();
        }

        private static string GetRowKey(string version)
        {
            return NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        }

        private static byte[] Serialize<T>(T newInfo) where T : IPackageWideEntity
        {
            return MessagePackSerializer.Serialize(newInfo, ExplorePackagesMessagePack.Options);
        }

        private static T Deserialize<T>(WideEntity entity)
        {
            return MessagePackSerializer.Deserialize<T>(entity.GetStream(), ExplorePackagesMessagePack.Options);
        }

        public interface IPackageWideEntity
        {
            DateTimeOffset CommitTimestamp { get; }
        }
    }
}
