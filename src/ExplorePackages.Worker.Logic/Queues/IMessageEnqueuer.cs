using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IMessageEnqueuer
    {
        Task EnqueueAsync<T>(IReadOnlyList<T> messages);
        Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, IReadOnlyList<T>> split);
        Task EnqueueAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore);
        Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages);
        Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore);
        QueueType GetQueueType<T>();
        QueueType GetQueueType(string schemaName);
        Task InitializeAsync();
    }
}