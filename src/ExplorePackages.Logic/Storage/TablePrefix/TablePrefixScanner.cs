using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.StorageUtility;

namespace Knapcode.ExplorePackages
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
            CloudTable table,
            string partitionKeyPrefix)
            where T : ITableEntity, new()
        {
            return await ListAsync<T>(table, partitionKeyPrefix, selectColumns: null, takeCount: MaxTakeCount);
        }

        public async Task<List<T>> ListAsync<T>(
            CloudTable table,
            string partitionKeyPrefix,
            IList<string> selectColumns,
            int takeCount)
            where T : ITableEntity, new()
        {
            _logger.LogInformation(
                "For table {TableName}, starting prefix scan with partition key prefix '{Prefix}', select columns '{SelectColumns}', and take count {TakeCount}",
                table.Name,
                partitionKeyPrefix,
                selectColumns,
                takeCount);

            var output = new List<T>();
            var parameters = new TableQueryParameters(table, selectColumns, takeCount);
            var start = new TablePrefixScanStart(parameters, partitionKeyPrefix);
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
                        output.AddRange(segment.Entities);
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                        newSteps = await ExecutePartitionKeyQueryAsync<T>(partitionKeyQuery);
                        break;
                    case TablePrefixScanPrefixQuery prefixQuery:
                        newSteps = await ExecutePrefixQueryAsync<T>(prefixQuery);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                foreach (var newRange in newSteps.Reverse())
                {
                    remainingSteps.Push(newRange);
                }
            }

            _logger.LogInformation("Completed prefix scan. Found {Count} entities.", output.Count);

            return output;
        }

        public List<TablePrefixScanStep> Start(TablePrefixScanStart start)
        {
            var steps = new List<TablePrefixScanStep>();
            var nextDepth = start.Depth + 1;

            // Originally I have a NULL character '\0' for both the row key and partition key prefix lower bound but
            // the Azure Storage Emulator behaved different for this. It looked like it completely ignored the '\0'
            // included in the query. Real Azure Table Storage does not ignore the NULL byte.
            if (start.PartitionKeyPrefix.Length > 0)
            {
                steps.Add(new TablePrefixScanPartitionKeyQuery(start.Parameters, nextDepth, start.PartitionKeyPrefix, rowKeySkip: null));
            }
            steps.Add(new TablePrefixScanPrefixQuery(start.Parameters, nextDepth, start.PartitionKeyPrefix, start.PartitionKeyPrefix));

            return steps;
        }

        public async Task<List<TablePrefixScanStep>> ExecutePartitionKeyQueryAsync<T>(TablePrefixScanPartitionKeyQuery query) where T : ITableEntity, new()
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            var filter = TableQuery.GenerateFilterCondition(
                PartitionKey,
                QueryComparisons.Equal, // Match the provided partition key
                query.PartitionKey);

            if (query.RowKeySkip != null)
            {
                filter = TableQuery.CombineFilters(
                    filter,
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition(
                        RowKey,
                        QueryComparisons.GreaterThan, // Skip past the provided row key
                        query.RowKeySkip));
            }

            var tableQuery = new TableQuery<T>
            {
                SelectColumns = query.Parameters.SelectColumns,
                TakeCount = MaxTakeCount,
                FilterString = filter,
            };

            var output = new List<TablePrefixScanStep>();
            TableContinuationToken continuationToken = null;

            do
            {
                TableQuerySegment<T> segment;
                using (metrics.TrackQuery())
                {
                    segment = await query.Parameters.Table.ExecuteQuerySegmentedAsync(tableQuery, continuationToken);
                }

                if (segment.Any())
                {
                    output.Add(new TablePrefixScanEntitySegment<T>(query.Parameters, query.Depth + 1, segment.Results));
                }

                continuationToken = segment.ContinuationToken;
            }
            while (continuationToken != null);

            return output;
        }

        public async Task<List<TablePrefixScanStep>> ExecutePrefixQueryAsync<T>(TablePrefixScanPrefixQuery query) where T : ITableEntity, new()
        {
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
            var upperBound = query.PartitionKeyPrefix + char.MaxValue;
            string lastPartitionKey = null;

            while (true)
            {
                var lowerBound = lastPartitionKey == null ? query.PartitionKeyLowerBound : IncrementPrefix(query.PartitionKeyPrefix, lastPartitionKey) + char.MaxValue;
                var filter = TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition(
                        PartitionKey,
                        QueryComparisons.GreaterThan, // Skip past the first character of last partition key seen.
                        lowerBound),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition(
                        PartitionKey,
                        QueryComparisons.LessThan, // Don't go outside of the provided prefix
                        upperBound));

                var tableQuery = new TableQuery<T>
                {
                    SelectColumns = query.Parameters.SelectColumns,
                    TakeCount = query.Parameters.TakeCount,
                    FilterString = filter,
                };

                // Find the next segment with at least one entity. I'm not entirely sure if it's possible to get zero
                // entities but have a continuation token, so let's protect against that.
                TableContinuationToken continuationToken;
                TableQuerySegment<T> segment = null;
                do
                {
                    continuationToken = segment?.ContinuationToken;
                    using (metrics.TrackQuery())
                    {
                        segment = await query.Parameters.Table.ExecuteQuerySegmentedAsync(tableQuery, continuationToken);
                    }
                }
                while (!segment.Any() && segment.ContinuationToken != null);

                if (!segment.Any())
                {
                    break;
                }

                lastPartitionKey = segment.Results.Last().PartitionKey;
                output.AddRange(MakeResults(query, segment));

                if (segment.ContinuationToken == null)
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

        private static IEnumerable<TablePrefixScanStep> MakeResults<T>(TablePrefixScanPrefixQuery step, TableQuerySegment<T> segment) where T : ITableEntity, new()
        {
            if (!segment.Results.Any())
            {
                throw new ArgumentException("The segment must have at least one entity.");
            }

            var nextDepth = step.Depth + 1;

            // Produce a terminal node for the discovered results.
            yield return new TablePrefixScanEntitySegment<T>(step.Parameters, nextDepth, segment.Results);

            if (segment.ContinuationToken != null)
            {
                var last = segment.Results.Last();

                // Find the remaining row keys for the partition key that straddles the current and subsequent page.
                // It's possible that this partition key has very few rows or even only has a single row meaning that this
                // yield query may not result in very many records. That's OK. We don't want to read too far into the
                // prefix in a serial fashion.
                yield return new TablePrefixScanPartitionKeyQuery(step.Parameters, nextDepth, last.PartitionKey, last.RowKey);

                // Expand the next prefix of the last partition key.
                var nextPrefix = IncrementPrefix(step.PartitionKeyPrefix, last.PartitionKey);
                yield return new TablePrefixScanPrefixQuery(step.Parameters, nextDepth, nextPrefix, last.PartitionKey);
            }
        }
    }
}
