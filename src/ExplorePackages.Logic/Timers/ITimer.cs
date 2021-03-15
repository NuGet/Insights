using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface ITimer
    {
        string Name { get; }
        TimeSpan Frequency { get; }
        bool IsEnabled { get; }
        Task<bool> IsRunningAsync();
        Task InitializeAsync();
        Task ExecuteAsync();
    }
}
