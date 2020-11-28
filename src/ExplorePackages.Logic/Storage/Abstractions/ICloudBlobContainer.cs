using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages
{
    public interface ICloudBlobContainer
    {
        Task CreateIfNotExistsAsync();
        Task DeleteIfExistsAsync();
        ICloudBlobWrapper GetBlobReference(string blobName);
        CloudAppendBlob GetAppendBlobReference(string blobName);
        ICloudBlockBlobWrapper GetBlockBlobReference(string blobName);
        Task<BlobResultSegment> ListBlobsSegmentedAsync(string prefix, BlobContinuationToken currentToken);
    }
}
