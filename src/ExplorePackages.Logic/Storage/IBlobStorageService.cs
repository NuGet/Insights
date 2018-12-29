using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IBlobStorageService
    {
        Task<bool> TryDownloadStreamAsync(string blobName, Stream destinationStream);
        Task UploadStreamAsync(string blobName, string contentType, Stream stream, AccessCondition accessCondition);
    }
}