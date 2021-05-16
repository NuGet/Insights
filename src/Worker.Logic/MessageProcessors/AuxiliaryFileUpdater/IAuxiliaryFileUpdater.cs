using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
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
