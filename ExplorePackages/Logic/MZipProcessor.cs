using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipProcessor : IPackageCommitProcessor<PackageEntity>
    {
        private readonly MZipStore _mZipStore;

        public MZipProcessor(MZipStore mZipStore)
        {
            _mZipStore = mZipStore;
        }

        public string CursorName => CursorNames.MZip;

        public IReadOnlyList<string> DependencyCursorNames { get; } = new[]
        {
            CursorNames.NuGetOrg.FlatContainer,
            CursorNames.CatalogToDatabase,
        };

        public Task<PackageEntity> InitializeItemAsync(PackageEntity package, CancellationToken token)
        {
            if (package.CatalogPackage.Deleted)
            {
                return Task.FromResult<PackageEntity>(null);
            }

            return Task.FromResult(package);
        }

        public async Task ProcessBatchAsync(IReadOnlyList<PackageEntity> batch)
        {
            foreach (var package in batch)
            {
                await _mZipStore.StoreMZipAsync(
                    package.PackageRegistration.Id,
                    package.Version,
                    CancellationToken.None);
            }
        }
    }
}
