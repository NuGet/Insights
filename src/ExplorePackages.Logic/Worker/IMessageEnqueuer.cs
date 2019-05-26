using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public interface IMessageEnqueuer
    {
        Task EnqueueAsync(IReadOnlyList<PackageQueryMessage> messages);
    }
}
