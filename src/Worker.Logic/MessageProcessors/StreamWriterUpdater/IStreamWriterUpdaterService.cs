using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.StreamWriterUpdater
{
    public interface IStreamWriterUpdaterService<T>
    {
        Task InitializeAsync();
        Task StartAsync(bool loop, TimeSpan notBefore);
        Task<bool> IsRunningAsync();
        bool IsEnabled { get; }
    }
}
