using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IMessageProcessor<T>
    {
        Task ProcessAsync(T message, int dequeueCount);
    }
}
