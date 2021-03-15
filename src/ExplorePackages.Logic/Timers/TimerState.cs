using System;

namespace Knapcode.ExplorePackages
{
    public record TimerState
    {
        public string Name { get; init; }
        public bool? IsEnabledInStorage { get; init; }
        public bool IsEnabledInConfig { get; init; }
        public bool IsRunning { get; init; }
        public DateTimeOffset? LastExecuted { get; init; }
        public TimeSpan Frequency { get; init; }
    }
}
