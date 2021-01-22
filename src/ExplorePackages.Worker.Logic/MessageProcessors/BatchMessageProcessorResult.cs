using System;
using System.Collections.Generic;
using System.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class BatchMessageProcessorResult<T>
    {
        public static BatchMessageProcessorResult<T> Empty { get; } = new BatchMessageProcessorResult<T>(new Dictionary<TimeSpan, IReadOnlyList<T>>());

        public BatchMessageProcessorResult(IEnumerable<T> tryAgainLater, TimeSpan notBefore)
        {
            TryAgainLater = new Dictionary<TimeSpan, IReadOnlyList<T>>
            {
                { notBefore, tryAgainLater.ToList() },
            };
        }

        public BatchMessageProcessorResult(IEnumerable<(T Message, TimeSpan NotBefore)> tryAgainLater)
        {
            TryAgainLater = tryAgainLater
                .ToLookup(x => x.NotBefore)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<T>)x.ToList());
        }

        public BatchMessageProcessorResult(IReadOnlyDictionary<TimeSpan, IReadOnlyList<T>> tryAgainLater)
        {
            TryAgainLater = tryAgainLater;
        }

        public IReadOnlyDictionary<TimeSpan, IReadOnlyList<T>> TryAgainLater { get; }
    }
}
