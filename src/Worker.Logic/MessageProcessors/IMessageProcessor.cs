using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface IMessageProcessor<T>
    {
        Task ProcessAsync(T message, long dequeueCount);
    }
}
