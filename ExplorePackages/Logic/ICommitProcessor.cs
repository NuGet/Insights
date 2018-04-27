using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ICommitProcessor<TEntity, TItem>
    {
        string CursorName { get; }
        IReadOnlyList<string> DependencyCursorNames { get; }
        int BatchSize { get; }
        Task<IReadOnlyList<TItem>> InitializeItemsAsync(IReadOnlyList<TEntity> entities, CancellationToken token);
        Task ProcessBatchAsync(IReadOnlyList<TItem> batch);
    }
}
