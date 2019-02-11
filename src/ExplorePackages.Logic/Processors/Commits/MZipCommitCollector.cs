using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipCommitCollector : CommitCollector<PackageEntity, PackageEntity, object>
    {
        public MZipCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            MZipCommitProcessor processor,
            CommitCollectorSequentialProgressService sequentialProgressService,
            ISingletonService singletonService,
            ILogger<MZipCommitCollector> logger) : base(
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
