using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker
{
    public class BatchMessageProcessorResult<T>
    {
        public BatchMessageProcessorResult(IReadOnlyList<T> failed)
        {
            Failed = failed;
        }

        public IReadOnlyList<T> Failed { get; }
    }
}
