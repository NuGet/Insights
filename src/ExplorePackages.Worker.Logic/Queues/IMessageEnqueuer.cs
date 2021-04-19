using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IMessageEnqueuer
    {
        Task EnqueueAsync<T>(QueueType queue, IReadOnlyList<T> messages);
        Task EnqueueAsync<T>(QueueType queue, IReadOnlyList<T> messages, Func<T, IReadOnlyList<T>> split);
        Task EnqueueAsync<T>(QueueType queue, IReadOnlyList<T> messages, TimeSpan notBefore);
        Task EnqueuePoisonAsync<T>(QueueType queue, IReadOnlyList<T> messages);
        Task EnqueuePoisonAsync<T>(QueueType queue, IReadOnlyList<T> messages, TimeSpan notBefore);
        Task InitializeAsync();
    }
}