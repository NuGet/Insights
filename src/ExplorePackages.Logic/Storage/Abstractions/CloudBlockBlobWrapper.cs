using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages
{
    public class CloudBlockBlobWrapper : CloudBlobWrapper, ICloudBlockBlobWrapper
    {
        private readonly CloudBlockBlob _inner;

        public CloudBlockBlobWrapper(CloudBlockBlob cloudBlockBlob) : base(cloudBlockBlob)
        {
            _inner = cloudBlockBlob;
        }

        public async Task UploadFromStreamAsync(Stream stream, AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext)
        {
            await _inner.UploadFromStreamAsync(stream, accessCondition, options, operationContext);
        }
    }
}
