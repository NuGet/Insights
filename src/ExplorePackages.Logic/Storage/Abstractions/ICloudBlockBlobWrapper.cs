using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages
{
    public interface ICloudBlockBlobWrapper : ICloudBlobWrapper
    {
        Task UploadFromStreamAsync(Stream stream, AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext);
    }
}
