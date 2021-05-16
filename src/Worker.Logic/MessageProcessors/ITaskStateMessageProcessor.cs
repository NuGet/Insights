using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface ITaskStateMessageProcessor<T> where T : ITaskStateMessage
    {
        Task<bool> ProcessAsync(T message, long dequeueCount);
    }
}
