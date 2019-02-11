using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipToDatabaseCommitCollector : CommitCollector<PackageEntity, PackageArchiveMetadata, object>
    {
        public MZipToDatabaseCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            MZipToDatabaseCommitProcessor processor,
            CommitCollectorSequentialProgressService sequentialProgressService,
            ISingletonService singletonService,
            ILogger<MZipToDatabaseCommitCollector> logger) : base(
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
