using System;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IFileStorageService
    {
        Task CopyMZipFileToBlobAsync(string id, string version);
        Task CopyNuspecFileToBlobAsync(string id, string version);
        Task DeleteMZipStreamAsync(string id, string version);
        Task DeleteNuspecStreamAsync(string id, string version);
        Task<Stream> GetMZipStreamOrNullAsync(string id, string version);
        Task<Stream> GetNuspecStreamOrNullAsync(string id, string version);
        Task StoreMZipStreamAsync(string id, string version, Func<Stream, Task> writeAsync);
        Task StoreNuspecStreamAsync(string id, string version, Func<Stream, Task> writeAsync);
    }
}