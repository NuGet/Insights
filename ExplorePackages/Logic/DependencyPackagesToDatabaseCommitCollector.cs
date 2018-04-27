using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependencyPackagesToDatabaseCommitCollector : CommitCollector<PackageRegistrationEntity, PackageDependencyEntity>
    {
        public DependencyPackagesToDatabaseCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageRegistrationEntity> enumerator,
            DependencyPackagesToDatabaseCommitProcessor processor,
            ILogger log) : base(cursorService, enumerator, processor, log)
        {
        }
    }
}
