using System.Threading.Tasks;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public interface IAuxiliaryFileUpdaterService<T>
    {
        Task InitializeAsync();
        Task<bool> StartAsync();
        Task<bool> IsRunningAsync();
        bool HasRequiredConfiguration { get; }
    }
}
