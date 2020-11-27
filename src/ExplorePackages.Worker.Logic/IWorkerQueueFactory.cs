using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IWorkerQueueFactory
    {
        Task InitializeAsync();
        CloudQueue GetQueue();
    }
}