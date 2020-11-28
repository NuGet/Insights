using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages
{
    public class CloudBlobWrapper : ICloudBlobWrapper
    {
        private readonly CloudBlob _inner;

        public CloudBlobWrapper(CloudBlob inner)
        {
            _inner = inner;
        }

        public BlobProperties Properties => _inner.Properties;

        public async Task DownloadToStreamAsync(Stream target)
        {
            await _inner.DownloadToStreamAsync(target);
        }

        public async Task<bool> ExistsAsync() => await _inner.ExistsAsync();
    }
}
