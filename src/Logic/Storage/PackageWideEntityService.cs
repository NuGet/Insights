// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using NuGet.Insights.WideEntities;
using NuGet.Versioning;

#nullable enable

namespace NuGet.Insights
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

        public async Task<IReadOnlyDictionary<ICatalogLeafItem, TOutput>> UpdateBatchAsync<TData, TOutput>(
            string tableName,
            string id,
            IReadOnlyCollection<ICatalogLeafItem> leafItems,
            Func<ICatalogLeafItem, Task<TOutput>> fetchOutputAsync,
            Func<TOutput, TData> outputToData,
            Func<TData, TOutput> dataToOutput)
            where TData : IPackageWideEntity
        {
            // This dictionary uses reference equality, not based on values.
            var adapterToLeafItem = leafItems.ToDictionary(x => (IPackageIdentityCommit)new CatalogLeafItemAdapter(x));

            var output = await UpdateBatchAsync(
                tableName,
                id,
                adapterToLeafItem.Keys,
                x => fetchOutputAsync(adapterToLeafItem[x]),
                outputToData,
                dataToOutput);

            return output.ToDictionary(x => adapterToLeafItem[x.Key], x => x.Value);
        }

        public async Task<IReadOnlyDictionary<IPackageIdentityCommit, TOutput>> UpdateBatchAsync<TData, TOutput>(
            string tableName,
            string id,
            IReadOnlyCollection<IPackageIdentityCommit> items,
            Func<IPackageIdentityCommit, Task<TOutput>> fetchOutputAsync,
            Func<TOutput, TData> outputToData,
            Func<TData, TOutput> dataToOutput)
            where TData : IPackageWideEntity
        {
            var rowKeyToItem = new Dictionary<string, IPackageIdentityCommit>();
            foreach (var item in items)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(id, item.PackageId))
                {
                    throw new ArgumentException("All items must have the same package ID.");
                }

                var rowKey = GetRowKey(item.PackageVersion);
                if (rowKeyToItem.ContainsKey(rowKey))
                {
                    throw new ArgumentException("The items must be unique by package version.");
                }

                rowKeyToItem.Add(rowKey, item);
            }

            var partitionKey = GetPartitionKey(id);

            // Fetch get the latest data for all items, where applicable. There are three possibilities for each
            // row keys:
            //   1. The row key does not exist. This means we must fetch the info and insert it into the table.
            //   2. The row exists but the data is stale. This means we must fetch the info and replace it in the table.
            //   3. The row exists and is not stale. We can just return the data in the table.
            var batch = new List<WideEntityOperation>();
            var output = new Dictionary<IPackageIdentityCommit, TOutput>();
            foreach (var (rowKey, item) in rowKeyToItem)
            {
                (var existingEntity, var matchingData) = await GetExistingAsync<TData>(tableName, partitionKey, rowKey, item);
                if (matchingData == null)
                {
                    var newOutput = await fetchOutputAsync(item);
                    var newBytes = Serialize(outputToData(newOutput));
                    if (existingEntity == null)
                    {
                        batch.Add(WideEntityOperation.Insert(partitionKey, rowKey, newBytes));
                    }
                    else
                    {
                        batch.Add(WideEntityOperation.Replace(existingEntity, newBytes));
                    }

                    output.Add(item, newOutput);
                }
                else
                {
                    output.Add(item, dataToOutput(matchingData));
                }
            }

            await _wideEntityService.ExecuteBatchAsync(tableName, batch, allowBatchSplits: true);

            return output;
        }

        public async Task<TOutput> GetOrUpdateInfoAsync<TData, TOutput>(
            string tableName,
            ICatalogLeafItem leafItem,
            Func<ICatalogLeafItem, Task<TOutput>> fetchOutputAsync,
            Func<TOutput, TData> outputToData,
            Func<TData, TOutput> dataToOutput)
            where TData : IPackageWideEntity
        {
            return await GetOrUpdateInfoAsync(
                tableName,
                new CatalogLeafItemAdapter(leafItem),
                x => fetchOutputAsync(leafItem),
                outputToData,
                dataToOutput);
        }

        public async Task<TOutput> GetOrUpdateInfoAsync<TData, TOutput>(
            string tableName,
            IPackageIdentityCommit item,
            Func<IPackageIdentityCommit, Task<TOutput>> fetchOutputAsync,
            Func<TOutput, TData> outputToData,
            Func<TData, TOutput> dataToOutput)
            where TData : IPackageWideEntity
        {
            var partitionKey = GetPartitionKey(item.PackageId);
            var rowKey = GetRowKey(item.PackageVersion);

            (var existingEntity, var matchingData) = await GetExistingAsync<TData>(tableName, partitionKey, rowKey, item);
            if (matchingData != null)
            {
                return dataToOutput(matchingData);
            }

            var newOutput = await fetchOutputAsync(item);
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

        private async Task<(WideEntity? ExistingEntity, T? MatchingInfo)> GetExistingAsync<T>(
            string tableName,
            string partitionKey,
            string rowKey,
            IPackageIdentityCommit item)
            where T : IPackageWideEntity
        {
            var existingEntity = await _wideEntityService.RetrieveAsync(tableName, partitionKey, rowKey);
            if (existingEntity != null)
            {
                var existingInfo = Deserialize<T>(existingEntity);

                // Prefer the existing entity if not older than the current item
                if (item.CommitTimestamp.HasValue
                    && existingInfo.CommitTimestamp.HasValue
                    && item.CommitTimestamp <= existingInfo.CommitTimestamp)
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
            return MessagePackSerializer.Serialize(newInfo, NuGetInsightsMessagePack.Options);
        }

        private static T Deserialize<T>(WideEntity entity)
        {
            return MessagePackSerializer.Deserialize<T>(entity.GetStream(), NuGetInsightsMessagePack.Options);
        }

        public interface IPackageWideEntity
        {
            DateTimeOffset? CommitTimestamp { get; }
        }

        private class CatalogLeafItemAdapter : IPackageIdentityCommit
        {
            private readonly ICatalogLeafItem _leafItem;

            public CatalogLeafItemAdapter(ICatalogLeafItem leafItem)
            {
                _leafItem = leafItem;
            }

            public string PackageId => _leafItem.PackageId;
            public string PackageVersion => _leafItem.PackageVersion;
            public DateTimeOffset? CommitTimestamp => _leafItem.CommitTimestamp;
        }
    }
}
