using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IRawMessageEnqueuer
    {
        BulkEnqueueStrategy BulkEnqueueStrategy { get; }
        Task InitializeAsync();
        Task AddAsync(IReadOnlyList<string> message);
        Task AddAsync(IReadOnlyList<string> messages, TimeSpan notBefore);
    }
}