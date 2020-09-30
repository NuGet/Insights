namespace Knapcode.ExplorePackages.Logic.Worker.BlobStorage
{
    public class AppendResultStorage
    {
        public AppendResultStorage(string container, int bucketCount)
        {
            Container = container;
            BucketCount = bucketCount;
        }

        public string Container { get; }
        public int BucketCount { get; }
    }
}
