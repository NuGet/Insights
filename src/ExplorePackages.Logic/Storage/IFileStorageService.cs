using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IFileStorageService
    {
        Task<Stream> GetStreamOrNullAsync(string id, string version, FileArtifactType type);
        Task StoreStreamAsync(string id, string version, FileArtifactType type, Func<Stream, Task> writeAsync, AccessCondition accessCondition);
    }
}