// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;
using static NuGet.Insights.StorageUtility;

namespace NuGet.Insights.TablePrefixScan
{
    public class TablePrefixScanner
    {
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<TablePrefixScanner> _logger;

        public TablePrefixScanner(
            ITelemetryClient telemetryClient,
            ILogger<TablePrefixScanner> logger)
        {
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task<List<T>> ListAsync<T>(
            TableClientWithRetryContext table,
            string partitionKeyPrefix)
            where T : class, ITableEntity, new()
        {
            return await ListAsync<T>(
                table,
                partitionKeyPrefix,
                selectColumns: null,
                takeCount: MaxTakeCount);
        }

        public async Task<List<T>> ListAsync<T>(
            TableClientWithRetryContext table,
            string partitionKeyPrefix,
            IList<string> selectColumns,
            int takeCount)
            where T : class, ITableEntity, new()
        {
            return await ListAsync<T>(
                table,
                partitionKeyPrefix,
                selectColumns,
                takeCount,
                expandPartitionKeys: true,
                segmentsPerFirstPrefix: 1,
                segmentsPerSubsequentPrefix: 1);
        }

        public async Task<List<T>> ListAsync<T>(
            TableClientWithRetryContext table,
            string partitionKeyPrefix,
            string partitionKeyLowerBound,
            string partitionKeyUpperBound,
            IList<string> selectColumns,
            int takeCount)
            where T : class, ITableEntity, new()
        {
            return await ListAsync<T>(
                table,
                partitionKeyPrefix,
                partitionKeyLowerBound,
                partitionKeyUpperBound,
                selectColumns,
                takeCount,
                expandPartitionKeys: true,
                segmentsPerFirstPrefix: 1,
                segmentsPerSubsequentPrefix: 1);
        }

        public async Task<List<T>> ListAsync<T>(
            TableClientWithRetryContext table,
            string partitionKeyPrefix,
            IList<string> selectColumns,
            int takeCount,
            bool expandPartitionKeys,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix)
            where T : class, ITableEntity, new()
        {
            var output = await ListAsync<T, T>(
                table,
                partitionKeyPrefix,
                partitionKeyLowerBound: null,
                partitionKeyUpperBound: null,
                selectColumns,
                takeCount,
                expandPartitionKeys,
                segmentsPerFirstPrefix,
                segmentsPerSubsequentPrefix,
                addSegment: (x, o) => o.AddRange(x));

            _logger.LogInformation("Completed prefix scan. Found {Count} entities.", output.Count);

            return output;
        }

        public async Task<List<T>> ListAsync<T>(
            TableClientWithRetryContext table,
            string partitionKeyPrefix,
            string partitionKeyLowerBound,
            string partitionKeyUpperBound,
            IList<string> selectColumns,
            int takeCount,
            bool expandPartitionKeys,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix)
            where T : class, ITableEntity, new()
        {
            var output = await ListAsync<T, T>(
                table,
                partitionKeyPrefix,
                partitionKeyLowerBound,
                partitionKeyUpperBound,
                selectColumns,
                takeCount,
                expandPartitionKeys,
                segmentsPerFirstPrefix,
                segmentsPerSubsequentPrefix,
                addSegment: (x, o) => o.AddRange(x));

            _logger.LogInformation("Completed prefix scan. Found {Count} entities.", output.Count);

            return output;
        }

        public async Task<List<IReadOnlyList<T>>> ListSegmentsAsync<T>(
            TableClientWithRetryContext table,
            string partitionKeyPrefix,
            IList<string> selectColumns,
            int takeCount,
            bool expandPartitionKeys,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix)
            where T : class, ITableEntity, new()
        {
            var output = await ListAsync<T, IReadOnlyList<T>>(
                table,
                partitionKeyPrefix,
                partitionKeyLowerBound: null,
                partitionKeyUpperBound: null,
                selectColumns,
                takeCount,
                expandPartitionKeys,
                segmentsPerFirstPrefix,
                segmentsPerSubsequentPrefix,
                addSegment: (x, o) => o.Add(x));

            _logger.LogInformation("Completed prefix scan. Found {Count} segments.", output.Count);

            return output;
        }

        private async Task<List<TOutput>> ListAsync<T, TOutput>(
            TableClientWithRetryContext table,
            string partitionKeyPrefix,
            string partitionKeyLowerBound,
            string partitionKeyUpperBound,
            IList<string> selectColumns,
            int takeCount,
            bool expandPartitionKeys,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix,
            Action<IReadOnlyList<T>, List<TOutput>> addSegment) where T : class, ITableEntity, new()
        {
            if (selectColumns != null && (!selectColumns.Contains(PartitionKey) || !selectColumns.Contains(RowKey)))
            {
                throw new ArgumentException($"The {PartitionKey} and {RowKey} columns must be queried.", nameof(selectColumns));
            }

            _logger.LogInformation(
                "Starting prefix scan with " +
                "partition key prefix '{Prefix}', " +
                "partition key lower bound '{LowerBound}', " +
                "partition key upper bound '{UpperBound}', " +
                "select columns '{SelectColumns}', " +
                "take count {TakeCount}, " +
                "segments per first prefix {SegmentsPerFirstPrefix}, " +
                "segments per subsequent prefix {SegmentsPerSubsequentPrefix}.",
                partitionKeyPrefix,
                partitionKeyLowerBound,
                partitionKeyUpperBound,
                selectColumns,
                takeCount,
                segmentsPerFirstPrefix,
                segmentsPerSubsequentPrefix);

            var output = new List<TOutput>();
            var parameters = new TableQueryParameters(table, selectColumns, takeCount, expandPartitionKeys);
            var start = new TablePrefixScanStart(parameters, partitionKeyPrefix, partitionKeyLowerBound, partitionKeyUpperBound);
            var initialSteps = Start(start);
            initialSteps.Reverse();
            var remainingSteps = new Stack<TablePrefixScanStep>(initialSteps);

            while (remainingSteps.Any())
            {
                var currentStep = remainingSteps.Pop();
                _logger.LogInformation("At depth {Depth}, processing prefix scan step: {Step}", currentStep.Depth, currentStep);

                IReadOnlyList<TablePrefixScanStep> newSteps;
                switch (currentStep)
                {
                    case TablePrefixScanEntitySegment<T> segment:
                        newSteps = Array.Empty<TablePrefixScanEntitySegment<T>>();
                        addSegment(segment.Entities, output);
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                        newSteps = await ExecutePartitionKeyQueryAsync<T>(partitionKeyQuery);
                        break;
                    case TablePrefixScanPrefixQuery prefixQuery:
                        newSteps = await ExecutePrefixQueryAsync<T>(prefixQuery, segmentsPerFirstPrefix, segmentsPerSubsequentPrefix);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                foreach (var newRange in newSteps.Reverse())
                {
                    remainingSteps.Push(newRange);
                }
            }

            return output;
        }

        public List<TablePrefixScanStep> Start(TablePrefixScanStart start)
        {
            var steps = new List<TablePrefixScanStep>();
            var nextDepth = start.Depth + 1;

            // Originally I have a NULL character '\0' for both the row key and partition key prefix lower bound but
            // the Azure Storage Emulator behaved different for this. It looked like it completely ignored the '\0'
            // included in the query. Real Azure Table Storage does not ignore the NULL byte.

            if (start.Parameters.ExpandPartitionKeys
                && (start.PartitionKeyLowerBound is null || string.CompareOrdinal(start.PartitionKeyPrefix, start.PartitionKeyLowerBound) > 0)
                && (start.PartitionKeyUpperBound is null || string.CompareOrdinal(start.PartitionKeyPrefix, start.PartitionKeyUpperBound) < 0))
            {
                steps.Add(new TablePrefixScanPartitionKeyQuery(start.Parameters, nextDepth, start.PartitionKeyPrefix, rowKeySkip: null));
            }

            var defaultLowerBound = start.PartitionKeyPrefix;
            var defaultUpperBound = start.PartitionKeyPrefix + char.MaxValue;

            steps.Add(new TablePrefixScanPrefixQuery(
                start.Parameters,
                nextDepth,
                partitionKeyPrefix: start.PartitionKeyPrefix,
                partitionKeyLowerBound: Max(defaultLowerBound, start.PartitionKeyLowerBound ?? defaultLowerBound),
                partitionKeyUpperBound: Min(defaultUpperBound, start.PartitionKeyUpperBound ?? defaultUpperBound)));

            return steps;
        }

        /*
        public List<TablePrefixScanStep> Start(TableRangeScanStart start)
        {
            var steps = new List<TablePrefixScanStep>();
            var nextDepth = start.Depth + 1;

            steps.Add(new TablePrefixScanPrefixQuery(
                start.Parameters,
                nextDepth,
                partitionKeyPrefix: string.Empty,
                partitionKeyLowerBound: start.PartitionKeyLowerBound,
                partitionKeyUpperBound: start.PartitionKeyUpperBound));

            return steps;
        }
        */

        public async Task<List<TablePrefixScanStep>> ExecutePartitionKeyQueryAsync<T>(TablePrefixScanPartitionKeyQuery query) where T : class, ITableEntity, new()
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            // Match the provided partition key
            Expression<Func<T, bool>> filter = x => x.PartitionKey == query.PartitionKey;

            if (query.RowKeySkip != null)
            {
                // Skip past the provided row key
                Expression<Func<T, bool>> rowKeySkip = x => x.RowKey.CompareTo(query.RowKeySkip) > 0;

                filter = Expression.Lambda<Func<T, bool>>(
                    Expression.AndAlso(filter.Body, rowKeySkip.Body),
                    filter.Parameters[0]);
            }

            var tablePageQuery = query.Parameters.Table
                .QueryAsync(
                    filter,
                    maxPerPage: MaxTakeCount,
                    select: query.Parameters.SelectColumns)
                .AsPages();

            var output = new List<TablePrefixScanStep>();

            await using var enumerator = tablePageQuery.GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync(metrics))
            {
                if (enumerator.Current.Values.Any())
                {
                    output.Add(new TablePrefixScanEntitySegment<T>(query.Parameters, query.Depth + 1, enumerator.Current.Values));
                }
            }

            return output;
        }

        public async Task<List<TablePrefixScanStep>> ExecutePrefixQueryAsync<T>(TablePrefixScanPrefixQuery query) where T : class, ITableEntity, new()
        {
            return await ExecutePrefixQueryAsync<T>(query, segmentsPerFirstPrefix: 1, segmentsPerSubsequentPrefix: 1);
        }

        public async Task<List<TablePrefixScanStep>> ExecutePrefixQueryAsync<T>(
            TablePrefixScanPrefixQuery query,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix)
            where T : class, ITableEntity, new()
        {
            if (segmentsPerFirstPrefix < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentsPerFirstPrefix), segmentsPerFirstPrefix, "The number of segments per first prefix must be at least 1.");
            }

            if (segmentsPerSubsequentPrefix < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentsPerSubsequentPrefix), segmentsPerSubsequentPrefix, "The number of segments per subsequent prefix must be at least 1.");
            }

            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            //
            // Consider the following query, where we're enumerating all partition keys starting with '$'.
            //
            //    QUERY = get 3 entities where PK > '$\0' and PK < '$\uffff'
            //
            //            +- PK --- RK -+
            //            | $1_0 | 10.0 |
            //   RESULT = | $1_0 | 11.0 | and a non-null continuation token
            //            | $2_A | 12.0 |
            //            +-------------+
            //
            // From the result set, we can deduce the following facts:
            //   1. The '$1' prefix is totally discovered. No more work in this space is necessary.
            //   2. The '$2_A' partition key exists but we don't know how many row keys are in it.
            //   3. The '$2' prefix exists but we don't know how many partition keys are in it.
            //
            // Therefore we yield three types of results from each fact.
            //   1. A terminal result, containing all three rows.
            //   2. A result to enumerate all of the row keys for partition key '$2_A', starting after row key '12.0'.
            //   3. A result to expand the '$2' prefix further, starting after partition key '$2_A'
            //
            // This method that we're in will also see that we've reached '$2_A' and will continue expanding the '$'
            // prefix to find partition keys '$3_A' and on with a query like this:
            //
            //    QUERY = get 3 entities where PK > '$2\uffff' and PK < '$\uffff'
            //
            var output = new List<TablePrefixScanStep>();
            var upperBound = query.PartitionKeyUpperBound;
            string lastPartitionKey = null;
            var segmentsPerPrefix = segmentsPerFirstPrefix;

            while (true)
            {
                var lowerBound = lastPartitionKey == null ? query.PartitionKeyLowerBound : IncrementPrefix(query.PartitionKeyPrefix, lastPartitionKey) + char.MaxValue;

                Expression<Func<T, bool>> filter = x =>
                    x.PartitionKey.CompareTo(lowerBound) > 0 // Skip past the first character of last partition key seen.
                    && x.PartitionKey.CompareTo(upperBound) < 0; // Don't go outside of the provided prefix

                var tablePageQuery = query.Parameters.Table
                    .QueryAsync(
                        filter,
                        maxPerPage: query.Parameters.TakeCount,
                        select: query.Parameters.SelectColumns)
                    .AsPages();

                // Find N segments with at least one entity. I'm not entirely sure if it's possible to get zero
                // entities but have a continuation token, so let's protect against that.
                var results = new List<T>();
                var segmentCount = 0;
                Page<T> segment = null;
                await using var enumerator = tablePageQuery.GetAsyncEnumerator();
                while (segmentCount < segmentsPerPrefix && await enumerator.MoveNextAsync(metrics))
                {
                    segment = enumerator.Current;
                    if (segment.Values.Any())
                    {
                        results.AddRange(segment.Values);
                        segmentCount++;
                    }
                }

                segmentsPerPrefix = segmentsPerSubsequentPrefix;

                if (!results.Any())
                {
                    break;
                }

                lastPartitionKey = results.Last().PartitionKey;
                var hasMore = segment.ContinuationToken != null;
                output.AddRange(MakeResults(query, results, hasMore));

                if (!hasMore)
                {
                    break;
                }
            }

            return output;
        }

        private static string IncrementPrefix(string partitionKeyPrefix, string partitionKey)
        {
            string nextChar;
            if (char.IsHighSurrogate(partitionKey[partitionKeyPrefix.Length]))
            {
                nextChar = partitionKey.Substring(partitionKeyPrefix.Length, 2);
            }
            else
            {
                nextChar = partitionKey.Substring(partitionKeyPrefix.Length, 1);
            }

            var prefix = partitionKeyPrefix + nextChar;
            return prefix;
        }

        private static IEnumerable<TablePrefixScanStep> MakeResults<T>(TablePrefixScanPrefixQuery step, List<T> results, bool hasMore) where T : ITableEntity, new()
        {
            if (!results.Any())
            {
                throw new ArgumentException("The segment must have at least one entity.");
            }

            var nextDepth = step.Depth + 1;

            // Produce a terminal node for the discovered results.
            yield return new TablePrefixScanEntitySegment<T>(step.Parameters, nextDepth, results);

            if (hasMore)
            {
                var last = results.Last();

                if (step.Parameters.ExpandPartitionKeys)
                {
                    // Find the remaining row keys for the partition key that straddles the current and subsequent page.
                    // It's possible that this partition key has very few rows or even only has a single row meaning that this
                    // yield query may not result in very many records. That's OK. We don't want to read too far into the
                    // prefix in a serial fashion.
                    yield return new TablePrefixScanPartitionKeyQuery(step.Parameters, nextDepth, last.PartitionKey, last.RowKey);
                }

                // Expand the next prefix of the last partition key.
                var nextPrefix = IncrementPrefix(step.PartitionKeyPrefix, last.PartitionKey);
                yield return new TablePrefixScanPrefixQuery(
                    step.Parameters,
                    nextDepth,
                    partitionKeyPrefix: nextPrefix,
                    partitionKeyLowerBound: Max(step.PartitionKeyLowerBound, last.PartitionKey),
                    partitionKeyUpperBound: Min(step.PartitionKeyUpperBound, nextPrefix + char.MaxValue));
            }
        }

        private static string Min(string a, string b)
        {
            return string.CompareOrdinal(a, b) < 0 ? a : b;
        }

        private static string Max(string a, string b)
        {
            return string.CompareOrdinal(a, b) > 0 ? a : b;
        }
    }
}
