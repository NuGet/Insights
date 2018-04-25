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

        public int BatchSize => 5000;

        public Task<IReadOnlyList<PackageEntity>> InitializeItemsAsync(IReadOnlyList<PackageEntity> packages, CancellationToken token)
        {
            var output = new List<PackageEntity>();

            foreach (var package in packages)
            {
                if (package.CatalogPackage.Deleted)
                {
                    continue;
                }

                output.Add(package);
            }

            return Task.FromResult<IReadOnlyList<PackageEntity>>(output);
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
