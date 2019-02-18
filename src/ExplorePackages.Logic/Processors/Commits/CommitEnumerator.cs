using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

    public class CommitEnumerator
    {
        private readonly ILogger<CommitEnumerator> _logger;

        public CommitEnumerator(ILogger<CommitEnumerator> logger)
        {
            _logger = logger;
        }

        public async Task<IReadOnlyList<TOutput>> GetCommitsAsync<TRecord, TOutput>(
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

        private async Task<IReadOnlyList<TOutput>> GetCommitsAsync<TRecord, TOutput>(
            GetRangeAsync<TRecord> getCommitRangeAsync,
            GetTimestamp<TRecord> getCommitTimestamp,
            GetOutput<TRecord, TOutput> getOutput,
            long start,
            long end,
            int originalBatchSize,
            int minimumRecords)
        {
            var currentBatchSize = originalBatchSize;
            var commits = new List<TOutput>();
            var totalFetched = 0;
            var fetched = 0;
            do
            {
                var items = await getCommitRangeAsync(
                    start,
                    end,
                    currentBatchSize);

                fetched = items.Count;

                var commitTimestampGroups = items
                    .GroupBy(x => getCommitTimestamp(x))
                    .ToDictionary(x => x.Key, x => (IReadOnlyList<TRecord>)x.ToList());

                var commitTimestamps = commitTimestampGroups
                    .Keys
                    .OrderBy(x => x)
                    .ToList();

                if (commitTimestamps.Count == 1 && fetched == currentBatchSize)
                {
                    // We've gotten a whole page but can't confidently move the start commit timestamp forward.
                    var newBatchSize = currentBatchSize + 1;
                    _logger.LogInformation(
                        "The batch size {OldBatchSize} is too small to move forward. A batch size of {NewBatchSize} will be attempted.",
                        currentBatchSize,
                        newBatchSize);
                    currentBatchSize = newBatchSize;
                }
                else
                {
                    totalFetched += fetched;

                    if (fetched < currentBatchSize)
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

                    currentBatchSize = originalBatchSize;
                }
            }
            while (fetched > 0 && start < end && totalFetched < minimumRecords);

            return commits;
        }

        private IReadOnlyList<TOutput> GetOutputList<TRecord, TOutput>(
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
