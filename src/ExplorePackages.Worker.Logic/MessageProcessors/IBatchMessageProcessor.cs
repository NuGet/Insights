using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IBatchMessageProcessor<T>
    {
        Task<BatchMessageProcessorResult<T>> ProcessAsync(IReadOnlyList<T> messages, int dequeueCount);
    }
}
