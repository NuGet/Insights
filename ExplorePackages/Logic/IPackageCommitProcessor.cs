using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IPackageCommitProcessor<T>
    {
        string CursorName { get; }
        IReadOnlyList<string> DependencyCursorNames { get; }
        Task<T> InitializeItemAsync(PackageEntity package, CancellationToken token);
        Task ProcessBatchAsync(IReadOnlyList<T> batch);
    }
}
