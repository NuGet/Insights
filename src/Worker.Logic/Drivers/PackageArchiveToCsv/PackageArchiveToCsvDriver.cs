// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Knapcode.MiniZip;

namespace NuGet.Insights.Worker.PackageArchiveToCsv
{
    public class PackageArchiveToCsvDriver :
        ZipArchiveMetadataToCsvDriver<PackageArchiveRecord, PackageArchiveEntry>,
        ICsvResultStorage<PackageArchiveRecord>,
        ICsvResultStorage<PackageArchiveEntry>
    {
        private readonly PackageFileService _packageFileService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageArchiveToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            PackageHashService packageHashService,
            IOptions<NuGetInsightsWorkerSettings> options) : base(catalogClient, packageHashService, options)
        {
            _packageFileService = packageFileService;
            _options = options;
        }

        string ICsvResultStorage<PackageArchiveRecord>.ResultContainerName => _options.Value.PackageArchiveContainerName;
        string ICsvResultStorage<PackageArchiveEntry>.ResultContainerName => _options.Value.PackageArchiveEntryContainerName;

        protected override bool NotFoundIsDeleted => true;
        protected override ArtifactFileType FileType => ArtifactFileType.Nupkg;

        protected override async Task<(ZipDirectory directory, long length, ILookup<string, string> headers)> GetZipDirectoryAndLengthAsync(IPackageIdentityCommit leafItem)
        {
            return await _packageFileService.GetZipDirectoryAndLengthAsync(leafItem);
        }

        protected override async Task InternalInitializeAsync()
        {
            await _packageFileService.InitializeAsync();
        }

        protected override PackageArchiveRecord NewArchiveDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
        {
            return new PackageArchiveRecord(scanId, scanTimestamp, leaf);
        }

        protected override PackageArchiveRecord NewArchiveDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return new PackageArchiveRecord(scanId, scanTimestamp, leaf);
        }

        protected override PackageArchiveEntry NewEntryDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
        {
            return new PackageArchiveEntry(scanId, scanTimestamp, leaf);
        }

        protected override PackageArchiveEntry NewEntryDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return new PackageArchiveEntry(scanId, scanTimestamp, leaf);
        }
    }
}
