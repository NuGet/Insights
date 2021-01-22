using System;
using System.Collections.Generic;
using System.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class BatchMessageProcessorResult<T>
    {
        public static BatchMessageProcessorResult<T> Empty { get; } = new BatchMessageProcessorResult<T>(
            failed: Array.Empty<T>(),
            tryAgainLater: new Dictionary<TimeSpan, IReadOnlyList<T>>());

        public BatchMessageProcessorResult(IEnumerable<T> failed)
        {
            Failed = failed.ToList();
            TryAgainLater = Empty.TryAgainLater;
        }

        public BatchMessageProcessorResult(IEnumerable<T> failed, IEnumerable<T> tryAgainLater, TimeSpan notBefore)
        {
            Failed = failed.ToList();
            TryAgainLater = new Dictionary<TimeSpan, IReadOnlyList<T>>
            {
                { notBefore, tryAgainLater.ToList() },
            };
        }

        public BatchMessageProcessorResult(IEnumerable<T> failed, IEnumerable<(T Message, TimeSpan NotBefore)> tryAgainLater)
        {
            Failed = failed.ToList();
            TryAgainLater = tryAgainLater
                .ToLookup(x => x.NotBefore)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<T>)x.ToList());
        }

        public BatchMessageProcessorResult(IEnumerable<T> failed, IReadOnlyDictionary<TimeSpan, IReadOnlyList<T>> tryAgainLater)
        {
            Failed = failed.ToList();
            TryAgainLater = tryAgainLater;
        }

        public IReadOnlyList<T> Failed { get; }
        public IReadOnlyDictionary<TimeSpan, IReadOnlyList<T>> TryAgainLater { get; }
    }
}
