using System;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IFileStorageService
    {
        Task CopyFileToBlobIfExistsAsync(string id, string version, FileArtifactType type);
        Task DeleteStreamAsync(string id, string version, FileArtifactType type);
        Task<Stream> GetStreamOrNullAsync(string id, string version, FileArtifactType type);
        Task StoreStreamAsync(string id, string version, FileArtifactType type, Func<Stream, Task> writeAsync);
    }
}