using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public delegate Task<IReadOnlyList<T>> GetRangeAsync<T>(
        long start,
        long end,
        int batchSize);

    public delegate long GetTimestamp<T>(T input);

    public delegate TOutput GetOutput<TRecord, TOutput>(
        long timestamp,
        IReadOnlyList<TRecord> records);

    public static class EnumeratorUtility
    {
        public static async Task<IReadOnlyList<TOutput>> GetCommitsAsync<TRecord, TOutput>(
               GetRangeAsync<TRecord> getCommitRangeAsync,
               GetTimestamp<TRecord> getCommitTimestamp,
               GetOutput<TRecord, TOutput> getOutput,
               DateTimeOffset start,
               DateTimeOffset end,
               int batchSize)
        {
            return await GetCommitsAsync(
                getCommitRangeAsync,
                getCommitTimestamp,
                getOutput,
                start.UtcTicks,
                end.UtcTicks,
                batchSize,
                minimumRecords: 1);
        }

        private static async Task<IReadOnlyList<TOutput>> GetCommitsAsync<TRecord, TOutput>(
            GetRangeAsync<TRecord> getCommitRangeAsync,
            GetTimestamp<TRecord> getCommitTimestamp,
            GetOutput<TRecord, TOutput> getOutput,
            long start,
            long end,
            int batchSize,
            int minimumRecords)
        {
            var commits = new List<TOutput>();
            var totalFetched = 0;
            int fetched;
            do
            {
                var items = await getCommitRangeAsync(
                    start,
                    end,
                    batchSize);

                fetched = items.Count;
                totalFetched += items.Count;

                var commitTimestampGroups = items
                    .GroupBy(x => getCommitTimestamp(x))
                    .ToDictionary(x => x.Key, x => (IReadOnlyList<TRecord>)x.ToList());

                var commitTimestamps = commitTimestampGroups
                    .Keys
                    .OrderBy(x => x)
                    .ToList();

                if (commitTimestamps.Count == 1 && fetched == batchSize)
                {
                    // We've gotten a whole page but can't confidently move the start commit timestamp forward.
                    throw new InvalidOperationException(
                        "Only one commit timestamp was encountered. A large page size is required to proceed.");
                }
                else if (fetched < batchSize)
                {
                    // We've reached the end and we have all of the last commit.
                    start = end;
                    commits.AddRange(GetOutputList(getOutput, commitTimestamps, commitTimestampGroups));
                }
                else if (fetched > 0)
                {
                    // Ignore the last commit timestamp since we might have a partial commit.
                    commitTimestamps.RemoveAt(commitTimestamps.Count - 1);
                    start = commitTimestamps.Last();
                    commits.AddRange(GetOutputList(getOutput, commitTimestamps, commitTimestampGroups));
                }
            }
            while (fetched > 0 && start < end && totalFetched < minimumRecords);

            return commits;
        }

        private static IReadOnlyList<TOutput> GetOutputList<TRecord, TOutput>(
            GetOutput<TRecord, TOutput> getOutput,
            IReadOnlyList<long> orderedTimestamps,
            IReadOnlyDictionary<long, IReadOnlyList<TRecord>> timestampToRecords)
        {
            var outputList = new List<TOutput>();
            foreach (var timestamp in orderedTimestamps)
            {
                outputList.Add(getOutput(timestamp, timestampToRecords[timestamp]));
            }

            return outputList;
        }
    }
}
