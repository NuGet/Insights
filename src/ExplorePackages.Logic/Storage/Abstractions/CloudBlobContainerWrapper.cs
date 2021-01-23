using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages
{
    public class CloudBlobContainerWrapper : ICloudBlobContainer
    {
        private readonly CloudBlobContainer _inner;

        public CloudBlobContainerWrapper(CloudBlobContainer inner)
        {
            _inner = inner;
        }

        public async Task CreateIfNotExistsAsync()
        {
            await _inner.CreateIfNotExistsAsync();
        }

        public async Task DeleteIfExistsAsync()
        {
            await _inner.DeleteIfExistsAsync();
        }

        public CloudAppendBlob GetAppendBlobReference(string blobName)
        {
            return _inner.GetAppendBlobReference(blobName);
        }

        public ICloudBlobWrapper GetBlobReference(string blobName)
        {
            return new CloudBlobWrapper(_inner.GetBlobReference(blobName));
        }

        public ICloudBlockBlobWrapper GetBlockBlobReference(string blobName)
        {
            return new CloudBlockBlobWrapper(_inner.GetBlockBlobReference(blobName));
        }

        public async Task<BlobResultSegment> ListBlobsSegmentedAsync(string prefix, BlobContinuationToken currentToken)
        {
            return await _inner.ListBlobsSegmentedAsync(prefix, currentToken);
        }
    }
}
