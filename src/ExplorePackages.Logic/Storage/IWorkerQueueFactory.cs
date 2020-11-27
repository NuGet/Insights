using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages
{
    public interface IWorkerQueueFactory
    {
        CloudQueue GetQueue();
    }
}