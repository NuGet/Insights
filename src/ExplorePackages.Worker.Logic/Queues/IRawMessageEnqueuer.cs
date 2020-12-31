using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IRawMessageEnqueuer
    {
        int MaxMessageSize { get; }
        BulkEnqueueStrategy BulkEnqueueStrategy { get; }
        Task<int> GetApproximateMessageCountAsync();
        Task<int> GetAvailableMessageCountLowerBoundAsync(int messageCount);
        Task<int> GetPoisonApproximateMessageCountAsync();
        Task<int> GetPoisonAvailableMessageCountLowerBoundAsync(int messageCount);
        Task InitializeAsync();
        Task AddAsync(IReadOnlyList<string> messages);
        Task AddAsync(IReadOnlyList<string> messages, TimeSpan notBefore);
        Task AddPoisonAsync(IReadOnlyList<string> messages);
        Task AddPoisonAsync(IReadOnlyList<string> messages, TimeSpan notBefore);
    }
}