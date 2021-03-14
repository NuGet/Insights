using System;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.StreamWriterUpdater
{
    public interface IStreamWriterUpdater<T> where T : IAsyncDisposable, IAsOfData
    {
        string OperationName { get; }
        string BlobName { get; }
        string ContainerName { get; }
        bool IsEnabled { get; }
        TimeSpan LoopFrequency { get; }
        Task<T> GetDataAsync();
        Task WriteAsync(T data, StreamWriter writer);
    }
}
