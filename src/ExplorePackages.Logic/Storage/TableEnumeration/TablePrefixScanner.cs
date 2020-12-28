using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.StorageUtility;

namespace Knapcode.ExplorePackages
{
    public class TablePrefixScanner
    {
        public static readonly IList<string> MinSelectColumns = new[] { PartitionKey, RowKey };

        public async Task<List<T>> EnumerateAllByPrefixAsync<T>(
            CloudTable table,
            string partitionKeyPrefix,
            IList<string> selectColumns)
            where T : ITableEntity, new()
        {
            var output = new List<T>();
            var initialSteps = StartEnumerateByPrefix(table, partitionKeyPrefix, selectColumns);
            initialSteps.Reverse();
            var remainingSteps = new Stack<TablePrefixScanStep>(initialSteps);

            while (remainingSteps.Any())
            {
                var currentStep = remainingSteps.Pop();

                IReadOnlyList<TablePrefixScanStep> newSteps;
                switch (currentStep)
                {
                    case TablePrefixScanEntitySegment<T> segment:
                        Console.Write(new string(' ', currentStep.Depth * 2));
                        Console.WriteLine(currentStep);
                        newSteps = Array.Empty<TablePrefixScanEntitySegment<T>>();
                        output.AddRange(segment.Entities);
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyStep:
                        Console.Write(new string(' ', currentStep.Depth * 2));
                        Console.WriteLine(currentStep);
                        newSteps = await EnumerateRowKeysAsync<T>(partitionKeyStep);
                        break;
                    case TablePrefixScanPrefixQuery expandStep:
                        Console.Write(new string(' ', currentStep.Depth * 2));
                        Console.WriteLine(currentStep);
                        newSteps = await ExpandPrefixAsync<T>(expandStep);
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

        private List<TablePrefixScanStep> StartEnumerateByPrefix(CloudTable table, string partitionKeyPrefix, IList<string> selectColumns)
        {
            var parameters = new TableQueryParameters(table, selectColumns, MaxTakeCount);
            var steps = new List<TablePrefixScanStep>();
            steps.Add(new TablePrefixScanPartitionKeyQuery(parameters, 0, partitionKeyPrefix, "\0"));
            steps.Add(new TablePrefixScanPrefixQuery(parameters, 0, partitionKeyPrefix, partitionKeyPrefix + "\0"));
            return steps;
        }

        private async Task<List<TablePrefixScanStep>> EnumerateRowKeysAsync<T>(TablePrefixScanPartitionKeyQuery query) where T : ITableEntity, new()
        {
            var filter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition(
                    PartitionKey,
                    QueryComparisons.Equal, // Match the provided partition key
                    query.PartitionKey),
                TableOperators.And,
                TableQuery.GenerateFilterCondition(
                    RowKey,
                    QueryComparisons.GreaterThan, // Skip past the provided row key
                    query.RowKeySkip));

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
                var segment = await query.Parameters.Table.ExecuteQuerySegmentedAsync(tableQuery, continuationToken);
                if (segment.Any())
                {
                    output.Add(new TablePrefixScanEntitySegment<T>(query.Parameters, query.Depth + 1, segment.Results));
                }

                continuationToken = segment.ContinuationToken;
            }
            while (continuationToken != null);

            return output;
        }

        private async Task<List<TablePrefixScanStep>> ExpandPrefixAsync<T>(TablePrefixScanPrefixQuery query) where T : ITableEntity, new()
        {
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
                    segment = await query.Parameters.Table.ExecuteQuerySegmentedAsync(tableQuery, continuationToken);
                }
                while (!segment.Any() && segment.ContinuationToken != null);

                if (!segment.Any())
                {
                    break;
                }

                lastPartitionKey = segment.Results.Last().PartitionKey;
                output.AddRange(MakeResults(query, segment));
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
                yield return new TablePrefixScanPartitionKeyQuery(step.Parameters, nextDepth, last.PartitionKey, last.RowKey);

                // Expand the next prefix of the last partition key.
                var nextPrefix = IncrementPrefix(step.PartitionKeyPrefix, last.PartitionKey);
                yield return new TablePrefixScanPrefixQuery(step.Parameters, nextDepth, nextPrefix, last.PartitionKey);
            }
        }
    }
}
