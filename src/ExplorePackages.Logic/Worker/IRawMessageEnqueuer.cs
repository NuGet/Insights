using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public interface IRawMessageEnqueuer
    {
        BulkEnqueueStrategy BulkEnqueueStrategy { get; }
        Task AddAsync(IReadOnlyList<string> message);
    }
}