using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependenciesToDatabaseCommitCollector : CommitCollector<PackageEntity, PackageDependencyGroups>
    {
        public DependenciesToDatabaseCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            DependenciesToDatabaseCommitProcessor processor,
            ILogger log) : base(cursorService, enumerator, processor, log)
        {
        }
    }
}
