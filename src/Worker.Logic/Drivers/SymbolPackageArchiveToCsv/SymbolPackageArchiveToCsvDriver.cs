// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Knapcode.MiniZip;

namespace NuGet.Insights.Worker.SymbolPackageArchiveToCsv
{
    public class SymbolPackageArchiveToCsvDriver :
        ZipArchiveMetadataToCsvDriver<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>,
        ICsvResultStorage<SymbolPackageArchiveRecord>,
        ICsvResultStorage<SymbolPackageArchiveEntry>
    {
        private readonly SymbolPackageFileService _symbolPackageFileService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public SymbolPackageArchiveToCsvDriver(
            CatalogClient catalogClient,
            SymbolPackageFileService symbolPackageFileService,
            SymbolPackageHashService symbolPackageHashService,
            IOptions<NuGetInsightsWorkerSettings> options) : base(catalogClient, symbolPackageHashService, options)
        {
            _symbolPackageFileService = symbolPackageFileService;
            _options = options;
        }

        string ICsvResultStorage<SymbolPackageArchiveRecord>.ResultContainerName => _options.Value.SymbolPackageArchiveContainerName;
        string ICsvResultStorage<SymbolPackageArchiveEntry>.ResultContainerName => _options.Value.SymbolPackageArchiveEntryContainerName;

        protected override bool NotFoundIsDeleted => false;
        protected override ArtifactFileType FileType => ArtifactFileType.Snupkg;

        protected override async Task<(ZipDirectory directory, long length, ILookup<string, string> headers)?> GetZipDirectoryAndLengthAsync(IPackageIdentityCommit leafItem)
        {
            return await _symbolPackageFileService.GetZipDirectoryAndLengthAsync(leafItem);
        }

        protected override async Task InternalInitializeAsync()
        {
            await _symbolPackageFileService.InitializeAsync();
        }

        protected override SymbolPackageArchiveRecord NewArchiveDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
        {
            return new SymbolPackageArchiveRecord(scanId, scanTimestamp, leaf);
        }

        protected override SymbolPackageArchiveRecord NewArchiveDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return new SymbolPackageArchiveRecord(scanId, scanTimestamp, leaf);
        }

        protected override SymbolPackageArchiveEntry NewEntryDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
        {
            return new SymbolPackageArchiveEntry(scanId, scanTimestamp, leaf);
        }

        protected override SymbolPackageArchiveEntry NewEntryDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return new SymbolPackageArchiveEntry(scanId, scanTimestamp, leaf);
        }
    }
}
