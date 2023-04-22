// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private readonly ITelemetryClient _telemetryClient;

        public PackageWideEntityService(
            WideEntityService wideEntityService,
            ITelemetryClient telemetryClient)
        {
            _wideEntityService = wideEntityService;
            _telemetryClient = telemetryClient;
        }

        public async Task InitializeAsync(string tableName)
        {
            await _wideEntityService.CreateTableAsync(tableName);
        }

        public async Task<IReadOnlyDictionary<TItem, TOutput>> UpdateBatchAsync<TItem, TData, TOutput>(
            string tableName,
            string id,
            IReadOnlyCollection<TItem> items,
            Func<TItem, Task<TOutput>> fetchOutputAsync,
            Func<TOutput, TData> outputToData,
            Func<TData, TOutput> dataToOutput)
            where TItem : IPackageIdentityCommit
            where TData : IPackageWideEntity
        {
            var rowKeyToItem = new Dictionary<string, TItem>();
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
            var output = new Dictionary<TItem, TOutput>();
            foreach (var (rowKey, item) in rowKeyToItem)
            {
                (var existingEntity, var matchingData) = await GetExistingAsync<TData>(
                    tableName,
                    partitionKey,
                    rowKey,
                    item,
                    forceUpdate: true);

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

        public async Task DeleteTableAsync(string tableName)
        {
            await _wideEntityService.DeleteTableAsync(tableName);
        }

        public async Task<TOutput> GetOrUpdateInfoAsync<TItem, TData, TOutput>(
            string tableName,
            TItem item,
            Func<TItem, Task<TOutput>> fetchOutputAsync,
            Func<TOutput, TData> outputToData,
            Func<TData, TOutput> dataToOutput)
            where TItem : IPackageIdentityCommit
            where TData : IPackageWideEntity
        {
            var partitionKey = GetPartitionKey(item.PackageId);
            var rowKey = GetRowKey(item.PackageVersion);

            (var existingEntity, var matchingData) = await GetExistingAsync<TData>(
                tableName,
                partitionKey,
                rowKey,
                item,
                forceUpdate: false);

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
            IPackageIdentityCommit item,
            bool forceUpdate)
            where T : IPackageWideEntity
        {
            var existingEntity = await _wideEntityService.RetrieveAsync(tableName, partitionKey, rowKey);

            void EmitMetric(CommitTimestampComparison comparison, bool useExisting)
            {
                var metric = _telemetryClient.GetMetric(
                    $"{nameof(PackageWideEntityService)}.{nameof(GetExistingAsync)}.Outcome",
                    "Type",
                    "ForceUpdate",
                    "TimestampComparison",
                    "UseExisting");

                metric.TrackValue(
                    1,
                    typeof(T).FullName,
                    forceUpdate ? "true" : "false",
                    comparison.ToString(),
                    useExisting ? "true" : "false");
            }

            void EmitMetricWithExisting(DateTimeOffset? existing, DateTimeOffset? incoming, bool useExisting)
            {
                var comparison = (existing.HasValue, incoming.HasValue) switch
                {
                    (false, false) => CommitTimestampComparison.BothAreNull,
                    (false, true) => CommitTimestampComparison.IncomingIsNull,
                    (true, false) => CommitTimestampComparison.ExistingIsNull,
                    (true, true) => existing > incoming ?
                        CommitTimestampComparison.ExistingIsLater :
                        existing < incoming ? CommitTimestampComparison.IncomingIsLater : CommitTimestampComparison.Equal,
                };

                EmitMetric(comparison, useExisting);
            }

            if (existingEntity != null)
            {
                var existingInfo = Deserialize<T>(existingEntity);

                // This is the core logic that handles the caching of package data in Azure Table Storage. Some data is
                // only updated on nuget.org along with a catalog event. These cases will always have non-null commit
                // timestamps in both the incoming item and the existing entity's commit timestamp. Other data can
                // change without any catalog leaf item being produced. In those cases we need to periodically check for
                // new data.
                //
                // If the incoming item has a non-null commit timestamp, that means a catalog leaf likely triggered the
                // this method call. This means the package owner or the site admin did something notable to the
                // package. If the incoming item has a null commit timestamp, it was triggered by some automated process
                // like a timer instead of the catalog. This means it's quite possible nothing changed related to this
                // package but we may want to check anyway (as indicated by the "force update" parameter).
                //
                // The incoming item can have a non-null or null commit timestamp. Same for the existing item. Also, the
                // caller can specify whether or not to force an update. This results in 8 possible cases.
                //
                // Case # | Force update | Existing timestamp | Incoming timestamp | Outcome
                // ------ | ------------ | ------------------ | ------------------ | -----------------------------------------
                // 1      | FALSE        | NULL               | NULL               | use existing
                // 2      | FALSE        | NON-NULL           | NULL               | use existing
                // 3      | FALSE        | NULL               | NON-NULL           | fetch data
                // 4      | FALSE        | NON-NULL           | NON-NULL           | fetch data if incoming timestamp is later
                // 5      | TRUE         | NULL               | NULL               | fetch data 
                // 6      | TRUE         | NON-NULL           | NULL               | fetch data
                // 7      | TRUE         | NULL               | NON-NULL           | fetch data
                // 8      | TRUE         | NON-NULL           | NON-NULL           | fetch data if incoming timestamp is later
                //
                // For case #1 and #2, we're not forcing an update and we have no indication that the existing data is
                // NOT stale. These are essentially cases where a reader wants to read the data quickly.
                //
                // For case #3, we can guess that the existing data is stale because a catalog event occurred some time
                // after a timer-based data update. In reality, this case should be very rare because it would only
                // happen if a reader catalog scan driver (using force update = FALSE) is triggered by a catalog event
                // that the writer catalog scan driver (using force update = TRUE) did not process. This should only
                // happen if a cursor dependency between the reader and the writer is not set up properly.
                //
                // For case #4, we may have an indication that the existing data is stale because the incoming timestamp
                // may be later than the existing one. The incoming timestamp being prior to the existing timestamp
                // should be rare for the same reasons as case #3.
                //
                // For case #5 and #6, we are forcing an update so we assuming the existing data IS stale. This is
                // essentially a case there the writer wants to ensure that the data gets updated every so often.
                //
                // For case #7, we're reacting to a catalog event and we want to force a data update for that reason.
                //
                // For case #8, we do have enough information to know whether the existing data is stale with respect to
                // the incoming item's commit timestamp.

                if (existingInfo.CommitTimestamp.HasValue && item.CommitTimestamp.HasValue && item.CommitTimestamp <= existingInfo.CommitTimestamp)
                {
                    // This is case #4 and #8
                    EmitMetricWithExisting(existingInfo.CommitTimestamp, item.CommitTimestamp, useExisting: true);
                    return (existingEntity, existingInfo);
                }
                else if (!forceUpdate)
                {
                    if (!item.CommitTimestamp.HasValue)
                    {
                        // This is case #1 and #2
                        EmitMetricWithExisting(existingInfo.CommitTimestamp, item.CommitTimestamp, useExisting: true);
                        return (existingEntity, existingInfo);
                    }
                    else
                    {
                        // This is case #3
                        EmitMetricWithExisting(existingInfo.CommitTimestamp, item.CommitTimestamp, useExisting: false);
                        return (existingEntity, default);
                    }
                }
                else
                {
                    // This is case #5, #6, and #7
                    EmitMetricWithExisting(existingInfo.CommitTimestamp, item.CommitTimestamp, useExisting: false);
                    return (existingEntity, default);
                }
            }
            else
            {
                EmitMetric(CommitTimestampComparison.NoExisting, useExisting: false);
                return (null, default);
            }
        }

        private enum CommitTimestampComparison
        {
            NoExisting,
            ExistingIsNull,
            IncomingIsNull,
            BothAreNull,
            ExistingIsLater,
            IncomingIsLater,
            Equal,
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
    }
}
