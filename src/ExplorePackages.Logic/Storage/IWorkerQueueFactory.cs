using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IWorkerQueueFactory
    {
        CloudQueue GetQueue();
    }
}