using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.StreamWriterUpdater
{
    public interface IStreamWriterUpdaterService<T>
    {
        Task InitializeAsync();
        Task<bool> StartAsync();
        Task<bool> IsRunningAsync();
        bool IsEnabled { get; }
    }
}
