// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker.PackageFileToCsv
{
    public class PackageFileToCsvDriver : ZipArchiveEntryHashToCsvDriver<PackageFileRecord>
    {
        private readonly FlatContainerClient _client;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageFileToCsvDriver(
            CatalogClient catalogClient,
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
            _client = client;
            _options = options;
        }

        public override string ResultContainerName => _options.Value.PackageFileContainerName;
        protected override bool UrlNotFoundIsDeleted => true;
        protected override ArtifactFileType FileType => ArtifactFileType.Nupkg;

        protected override async Task<string?> GetZipUrlAsync(CatalogLeafScan leafScan)
        {
            return await _client.GetPackageContentUrlAsync(leafScan.PackageId, leafScan.PackageVersion);
        }

        protected override Task InternalInitializeAsync()
        {
            return Task.CompletedTask;
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
