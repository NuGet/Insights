using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecCommitCollector : CommitCollector<PackageEntity, PackageEntity, object>
    {
        public NuspecCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            NuspecCommitProcessor processor,
            CommitCollectorSequentialProgressService sequentialProgressService,
            ISingletonService singletonService,
            ILogger<NuspecCommitCollector> logger) : base(
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
