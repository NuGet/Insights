using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IBlobStorageService
    {
        bool IsEnabled { get; }
        Task<Stream> GetStreamOrNullAsync(string blobName);
        Task UploadStreamAsync(string blobName, string contentType, Stream stream);
    }
}