using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependenciesToDatabaseCommitCollector : CommitCollector<PackageEntity, PackageDependencyGroups>
    {
        public DependenciesToDatabaseCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            DependenciesToDatabaseCommitProcessor processor,
            ILogger<DependenciesToDatabaseCommitCollector> logger) : base(cursorService, enumerator, processor, logger)
        {
        }
    }
}
