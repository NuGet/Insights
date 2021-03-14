using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Timers
{
    public interface ITimer
    {
        string Name { get; }
        TimeSpan Frequency { get; }
        bool IsEnabled { get; }
        Task InitializeAsync();
        Task ExecuteAsync();
    }
}
