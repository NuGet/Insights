using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IWorkerQueueFactory
    {
        CloudQueue GetQueue();
    }
}