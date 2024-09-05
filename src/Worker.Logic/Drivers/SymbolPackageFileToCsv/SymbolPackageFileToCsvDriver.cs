// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Knapcode.MiniZip;

namespace NuGet.Insights.Worker.SymbolPackageFileToCsv
{
    public class SymbolPackageFileToCsvDriver : FullZipArchiveEntryToCsvDriver<SymbolPackageFileRecord>
    {
        private readonly SymbolPackageFileService _fileService;
        private readonly SymbolPackageClient _client;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public SymbolPackageFileToCsvDriver(
            CatalogClient catalogClient,
            SymbolPackageHashService hashService,
            SymbolPackageFileService fileService,
            SymbolPackageClient client,
            FileDownloader fileDownloader,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<SymbolPackageFileToCsvDriver> logger)
            : base(
                  catalogClient,
                  fileDownloader,
                  hashService,
                  logger)
        {
            _fileService = fileService;
            _client = client;
            _options = options;
        }

        public override string ResultContainerName => _options.Value.SymbolPackageFileContainerName;
        protected override bool NotFoundIsDeleted => false;
        protected override ArtifactFileType FileType => ArtifactFileType.Snupkg;

        protected override async Task<ZipDirectory?> GetZipDirectoryAsync(IPackageIdentityCommit leafItem)
        {
            var info = await _fileService.GetZipDirectoryAndLengthAsync(leafItem);
            if (!info.HasValue)
            {
                return null;
            }

            return info.Value.Directory;
        }

        protected override Task<string> GetZipUrlAsync(CatalogLeafScan leafScan)
        {
            return Task.FromResult(_client.GetSymbolPackageUrl(leafScan.PackageId, leafScan.PackageVersion));
        }

        protected override async Task InternalInitializeAsync()
        {
            await _fileService.InitializeAsync();
        }

        protected override SymbolPackageFileRecord NewDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
        {
            return new SymbolPackageFileRecord(scanId, scanTimestamp, leaf);
        }

        protected override SymbolPackageFileRecord NewDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return new SymbolPackageFileRecord(scanId, scanTimestamp, leaf);
        }
    }
}
