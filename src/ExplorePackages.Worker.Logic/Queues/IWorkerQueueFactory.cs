using System.Threading.Tasks;
using Azure.Storage.Queues;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IWorkerQueueFactory
    {
        Task InitializeAsync();
        Task<QueueClient> GetQueueAsync(QueueType type);
        Task<QueueClient> GetPoisonQueueAsync(QueueType type);
    }
}