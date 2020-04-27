namespace Knapcode.ExplorePackages.Worker
{
    public class QueueStorageEnqueuerConfiguration
    {
        public bool UseBulkEnqueueStrategy { get; set; } = false;
        public int BulkEnqueueThreshold { get; set; } = 2;
        public int EnqueueWorkers { get; set; } = 1;
    }
}
