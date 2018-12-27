using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipToDatabaseCommitCollector : CommitCollector<PackageEntity, PackageArchiveMetadata>
    {
        public MZipToDatabaseCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            MZipToDatabaseCommitProcessor processor,
            ISingletonService singletonService,
            ILogger<MZipToDatabaseCommitCollector> logger) : base(
                cursorService,
                enumerator,
                processor,
                singletonService,
                logger)
        {
        }
    }
}
