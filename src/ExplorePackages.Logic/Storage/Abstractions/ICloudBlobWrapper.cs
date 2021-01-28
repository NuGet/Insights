using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages
{
    public interface ICloudBlobWrapper
    {
        BlobProperties Properties { get; }
        Task<bool> ExistsAsync();
        Task DownloadToStreamAsync(Stream stream);
        Task<Stream> OpenReadAsync();
    }
}
