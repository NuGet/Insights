using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface IRawMessageEnqueuer
    {
        int MaxMessageSize { get; }
        BulkEnqueueStrategy BulkEnqueueStrategy { get; }
        Task<int> GetApproximateMessageCountAsync(QueueType queue);
        Task<int> GetAvailableMessageCountLowerBoundAsync(QueueType queue, int messageCount);
        Task<int> GetPoisonApproximateMessageCountAsync(QueueType queue);
        Task<int> GetPoisonAvailableMessageCountLowerBoundAsync(QueueType queue, int messageCount);
        Task InitializeAsync();
        Task AddAsync(QueueType queue, IReadOnlyList<string> messages);
        Task AddAsync(QueueType queue, IReadOnlyList<string> messages, TimeSpan notBefore);
        Task AddPoisonAsync(QueueType queue, IReadOnlyList<string> messages);
        Task AddPoisonAsync(QueueType queue, IReadOnlyList<string> messages, TimeSpan notBefore);
    }
}