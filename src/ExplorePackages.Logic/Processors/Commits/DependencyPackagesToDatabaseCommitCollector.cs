using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependencyPackagesToDatabaseCommitCollector : CommitCollector<PackageRegistrationEntity, PackageDependencyEntity, DependencyPackagesToDatabaseCommitProcessor.ProgressToken>
    {
        public DependencyPackagesToDatabaseCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageRegistrationEntity> enumerator,
            DependencyPackagesToDatabaseCommitProcessor processor,
            CommitCollectorSequentialProgressService sequentialProgressService,
            ISingletonService singletonService,
            ILogger<DependencyPackagesToDatabaseCommitCollector> logger) : base(
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
