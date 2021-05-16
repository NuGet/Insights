using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface IBatchMessageProcessor<T>
    {
        Task<BatchMessageProcessorResult<T>> ProcessAsync(IReadOnlyList<T> messages, long dequeueCount);
    }
}
