using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ICommitProcessor<TEntity, TItem, TProgressToken>
    {
        string CursorName { get; }
        IReadOnlyList<string> DependencyCursorNames { get; }
        int BatchSize { get; }
        ProcessMode ProcessMode { get; }
        Task<ItemBatch<TItem, TProgressToken>> InitializeItemsAsync(IReadOnlyList<TEntity> entities, TProgressToken progressToken, CancellationToken token);
        string SerializeProgressToken(TProgressToken progressToken);
        TProgressToken DeserializeProgressToken(string serializedProgressToken);
        Task ProcessBatchAsync(IReadOnlyList<TItem> batch);
    }
}
