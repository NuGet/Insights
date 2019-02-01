using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipToDatabaseCommitProcessor : ICommitProcessor<PackageEntity, PackageArchiveMetadata>
    {
        private readonly MZipStore _mZipStore;
        private readonly IPackageService _packageService;
        private readonly IBatchSizeProvider _batchSizeProvider;

        public MZipToDatabaseCommitProcessor(
            MZipStore mZipStore,
            IPackageService packageService,
            IBatchSizeProvider batchSizeProvider)
        {
            _mZipStore = mZipStore;
            _packageService = packageService;
            _batchSizeProvider = batchSizeProvider;
        }

        public string CursorName => CursorNames.MZipToDatabase;

        public IReadOnlyList<string> DependencyCursorNames { get; } = new[]
        {
            CursorNames.CatalogToDatabase,
            CursorNames.MZip,
        };

        public int BatchSize => _batchSizeProvider.Get(BatchSizeType.MZipToDatabase);

        public async Task<ItemBatch<PackageArchiveMetadata>> InitializeItemsAsync(
            IReadOnlyList<PackageEntity> packages,
            int skip,
            CancellationToken token)
        {
            var output = await TaskProcessor.ExecuteAsync(
                packages,
                x => InitializeItemAsync(x, token),
                workerCount: 32,
                token: token);

            var list = output.Where(x => x != null).ToList();

            return new ItemBatch<PackageArchiveMetadata>(list, hasMoreItems: false);
        }

        private async Task<PackageArchiveMetadata> InitializeItemAsync(PackageEntity package, CancellationToken token)
        {
            // Read the .zip directory.
            var context = await _mZipStore.GetMZipContextAsync(
                package.PackageRegistration.Id,
                package.Version);

            if (!context.Exists)
            {
                if (!package.CatalogPackage.Deleted)
                {
                    throw new InvalidOperationException($"Could not find .mzip for {package.PackageRegistration.Id} {package.Version}.");
                }

                return null;
            }

            // Gather the metadata.
            var metadata = new PackageArchiveMetadata
            {
                Id = package.PackageRegistration.Id,
                Version = package.Version,
                Size = context.Size.Value,
                ZipDirectory = context.ZipDirectory,
            };

            return metadata;
        }

        public async Task ProcessBatchAsync(IReadOnlyList<PackageArchiveMetadata> batch)
        {
            await _packageService.AddOrUpdatePackagesAsync(batch);
        }
    }
}
