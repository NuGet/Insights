// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Knapcode.MiniZip;

namespace NuGet.Insights.Worker.PackageFileToCsv
{
    public class PackageFileToCsvDriver : FullZipArchiveEntryToCsvDriver<PackageFileRecord>
    {
        private readonly PackageFileService _fileService;
        private readonly FlatContainerClient _client;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageFileToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService fileService,
            PackageHashService hashService,
            FlatContainerClient client,
            FileDownloader fileDownloader,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<PackageFileToCsvDriver> logger)
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

        public override string ResultContainerName => _options.Value.PackageFileContainer;
        protected override bool NotFoundIsDeleted => true;
        protected override ArtifactFileType FileType => ArtifactFileType.Nupkg;

        protected override async Task<ZipDirectory?> GetZipDirectoryAsync(IPackageIdentityCommit leafItem)
        {
            var info = await _fileService.GetZipDirectoryAndLengthAsync(leafItem);
            if (!info.HasValue)
            {
                return null;
            }

            return info.Value.Directory;
        }

        protected override async Task<string> GetZipUrlAsync(CatalogLeafScan leafScan)
        {
            return await _client.GetPackageContentUrlAsync(leafScan.PackageId, leafScan.PackageVersion);
        }

        protected override async Task InternalInitializeAsync()
        {
            await _fileService.InitializeAsync();
        }

        protected override PackageFileRecord NewDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
        {
            return new PackageFileRecord(scanId, scanTimestamp, leaf);
        }

        protected override PackageFileRecord NewDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return new PackageFileRecord(scanId, scanTimestamp, leaf);
        }
    }
}
