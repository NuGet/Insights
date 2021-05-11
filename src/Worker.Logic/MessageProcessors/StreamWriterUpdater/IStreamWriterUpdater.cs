using System;
using System.IO;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.BuildVersionSet;

namespace Knapcode.ExplorePackages.Worker.StreamWriterUpdater
{
    public interface IStreamWriterUpdater<T> where T : IAsOfData
    {
        string OperationName { get; }
        string BlobName { get; }
        string ContainerName { get; }
        Type RecordType { get; }
        bool IsEnabled { get; }
        bool AutoStart { get; }
        TimeSpan Frequency { get; }
        Task<T> GetDataAsync();
        Task WriteAsync(IVersionSet versionSet, T data, StreamWriter writer);
    }
}
