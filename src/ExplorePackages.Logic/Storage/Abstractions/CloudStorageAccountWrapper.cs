using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages
{
    public class CloudStorageAccountWrapper : ICloudStorageAccount
    {
        private readonly CloudStorageAccount _inner;

        public CloudStorageAccountWrapper(CloudStorageAccount inner)
        {
            _inner = inner;
        }

        public ICloudBlobClient CreateCloudBlobClient()
        {
            return new CloudBlobClientWrapper(_inner.CreateCloudBlobClient());
        }

        public ICloudTableClient CreateCloudTableClient()
        {
            return new CloudTableClientWrapper(_inner.CreateCloudTableClient());
        }
    }
}
