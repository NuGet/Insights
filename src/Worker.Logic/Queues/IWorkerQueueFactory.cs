using System.Threading.Tasks;
using Azure.Storage.Queues;

namespace NuGet.Insights.Worker
{
    public interface IWorkerQueueFactory
    {
        Task InitializeAsync();
        Task<QueueClient> GetQueueAsync(QueueType type);
        Task<QueueClient> GetPoisonQueueAsync(QueueType type);
    }
}