using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ITaskStateMessageProcessor<T> where T : ITaskStateMessage
    {
        Task<bool> ProcessAsync(T message, long dequeueCount);
    }
}
