using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipCommitProcessor : ICommitProcessor<PackageEntity, PackageEntity>
    {
        private readonly MZipStore _mZipStore;

        public MZipCommitProcessor(MZipStore mZipStore)
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

        public Task<ItemBatch<PackageEntity>> InitializeItemsAsync(
            IReadOnlyList<PackageEntity> packages,
            int skip,
            CancellationToken token)
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

            return Task.FromResult(new ItemBatch<PackageEntity>(
                output,
                hasMoreItems: false));
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
