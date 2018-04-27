using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipToDatabaseCommitCollector : CommitCollector<PackageEntity, PackageArchiveMetadata>
    {
        public MZipToDatabaseCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            MZipToDatabaseCommitProcessor processor,
            ILogger log) : base(cursorService, enumerator, processor, log)
        {
        }
    }
}
