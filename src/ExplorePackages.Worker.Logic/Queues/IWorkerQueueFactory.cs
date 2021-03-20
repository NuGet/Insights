using System.Threading.Tasks;
using Azure.Storage.Queues;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IWorkerQueueFactory
    {
        Task InitializeAsync();
        Task<QueueClient> GetQueueAsync();
        Task<QueueClient> GetPoisonQueueAsync();
    }
}