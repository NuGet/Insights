using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private readonly CloudBlobClient _inner;

        public CloudBlobClientWrapper(CloudBlobClient inner)
        {
            _inner = inner;
        }

        public ICloudBlobContainer GetContainerReference(string containerName)
        {
            return new CloudBlobContainerWrapper(_inner.GetContainerReference(containerName));
        }
    }
}
