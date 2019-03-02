using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecsCommitCollector : CommitCollector<PackageEntity, PackageEntity, object>
    {
        public NuspecsCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            NuspecsCommitProcessor processor,
            CommitCollectorSequentialProgressService sequentialProgressService,
            ISingletonService singletonService,
            ILogger<NuspecsCommitCollector> logger) : base(
                cursorService,
                enumerator,
                processor,
                sequentialProgressService,
                singletonService,
                logger)
        {
        }
    }
}
