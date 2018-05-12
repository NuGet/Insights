using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipCommitCollector : CommitCollector<PackageEntity, PackageEntity>
    {
        public MZipCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            MZipCommitProcessor processor,
            ILogger<MZipCommitCollector> logger) : base(cursorService, enumerator, processor, logger)
        {
        }
    }
}
