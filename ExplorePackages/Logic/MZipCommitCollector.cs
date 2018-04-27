using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipCommitCollector : CommitCollector<PackageEntity, PackageEntity>
    {
        public MZipCommitCollector(
            CursorService cursorService,
            ICommitEnumerator<PackageEntity> enumerator,
            MZipCommitProcessor processor,
            ILogger log) : base(cursorService, enumerator, processor, log)
        {
        }
    }
}
