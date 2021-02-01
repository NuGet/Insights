using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ILoopingMessageProcessor<T> where T : ILoopingMessage
    {
        string LeaseName { get; }
        Task StartAsync();
        Task<bool> ProcessAsync(T message, int dequeueCount);
    }
}
