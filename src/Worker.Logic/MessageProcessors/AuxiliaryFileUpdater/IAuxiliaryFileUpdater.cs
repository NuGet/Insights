using System;
using System.IO;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.BuildVersionSet;

namespace Knapcode.ExplorePackages.Worker.AuxiliaryFileUpdater
{
    public interface IAuxiliaryFileUpdater<T> where T : IAsOfData
    {
        string OperationName { get; }
        string BlobName { get; }
        string ContainerName { get; }
        Type RecordType { get; }
        bool HasRequiredConfiguration { get; }
        bool AutoStart { get; }
        TimeSpan Frequency { get; }
        Task<T> GetDataAsync();
        Task WriteAsync(IVersionSet versionSet, T data, StreamWriter writer);
    }
}
