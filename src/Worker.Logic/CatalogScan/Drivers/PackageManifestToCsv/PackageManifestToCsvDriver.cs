// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Insights.Worker.PackageManifestToCsv
{
    public class PackageManifestToCsvDriver : ICatalogLeafToCsvDriver<PackageManifestRecord>, ICsvResultStorage<PackageManifestRecord>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageManifestService _packageManifestService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageManifestToCsvDriver(
            CatalogClient catalogClient,
            PackageManifestService packageManifestService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _packageManifestService = packageManifestService;
            _options = options;
        }

        public string ResultContainerName => _options.Value.PackageManifestContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageManifestRecord> Prune(List<PackageManifestRecord> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public async Task InitializeAsync()
        {
            await _packageManifestService.InitializeAsync();
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSet<PackageManifestRecord>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var records = await ProcessLeafInternalAsync(leafScan);
            return DriverResult.Success(new CsvRecordSet<PackageManifestRecord>(PackageRecord.GetBucketKey(leafScan), records));
        }

        private async Task<List<PackageManifestRecord>> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return new List<PackageManifestRecord> { new PackageManifestRecord(scanId, scanTimestamp, leaf) };
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var result = await _packageManifestService.GetNuspecReaderAndSizeAsync(leafScan);
                if (result == null)
                {
                    // Ignore packages where the .nuspec is missing. A subsequent scan will produce a deleted asset record.
                    return new List<PackageManifestRecord>();
                }

                return new List<PackageManifestRecord> { GetRecord(scanId, scanTimestamp, leaf, result.Value.NuspecReader, result.Value.ManifestLength) };
            }
        }

        private PackageManifestRecord GetRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, NuspecReader nuspecReader, int size)
        {
            var record = new PackageManifestRecord(scanId, scanTimestamp, leaf)
            {
                Size = size,

                // From NuspecCoreReaderBase
                DevelopmentDependency = nuspecReader.GetDevelopmentDependency(),
                OriginalId = nuspecReader.GetId(),
                MinClientVersion = nuspecReader.GetMinClientVersion()?.ToNormalizedString(),
                PackageTypes = KustoDynamicSerializer.Serialize(nuspecReader.GetPackageTypes()),
                OriginalVersion = nuspecReader.GetVersion().OriginalVersion,
                IsServiceable = nuspecReader.IsServiceable(),

                // From NuspecReader
                Authors = nuspecReader.GetAuthors(),
                Copyright = nuspecReader.GetCopyright(),
                Description = nuspecReader.GetDescription(),
                FrameworkAssemblyGroups = KustoDynamicSerializer.Serialize(nuspecReader.GetFrameworkAssemblyGroups().ToList()),
                FrameworkRefGroups = KustoDynamicSerializer.Serialize(nuspecReader.GetFrameworkRefGroups().ToList()),
                Icon = nuspecReader.GetIcon(),
                IconUrl = nuspecReader.GetIconUrl(),
                Language = nuspecReader.GetLanguage(),
                LicenseUrl = nuspecReader.GetLicenseUrl(),
                Owners = nuspecReader.GetOwners(),
                ProjectUrl = nuspecReader.GetProjectUrl(),
                Readme = nuspecReader.GetReadme(),
                ReferenceGroups = KustoDynamicSerializer.Serialize(nuspecReader.GetReferenceGroups().ToList()),
                ReleaseNotes = nuspecReader.GetReleaseNotes(),
                RequireLicenseAcceptance = nuspecReader.GetRequireLicenseAcceptance(),
                Summary = nuspecReader.GetSummary(),
                Tags = nuspecReader.GetTags(),
                Title = nuspecReader.GetTitle(),
            };

            ReadLicenseMetadata(nuspecReader, record);
            ReadRepositoryMetadata(nuspecReader, record);
            ReadContentFiles(nuspecReader, record);
            ReadDependencyGroups(nuspecReader, record);
            SplitTags(record);

            return record;
        }

        private static void SplitTags(PackageManifestRecord record)
        {
            var splitTags = string.IsNullOrWhiteSpace(record.Tags) ? Array.Empty<string>() : Utils.SplitTags(record.Tags);
            record.SplitTags = KustoDynamicSerializer.Serialize(splitTags);
        }

        private static void ReadLicenseMetadata(NuspecReader nuspecReader, PackageManifestRecord record)
        {
            var metadata = nuspecReader.GetLicenseMetadata();
            if (metadata == null)
            {
                return;
            }

            record.LicenseMetadata = KustoDynamicSerializer.Serialize(metadata);
        }

        private static void ReadRepositoryMetadata(NuspecReader nuspecReader, PackageManifestRecord record)
        {
            var metadata = nuspecReader.GetRepositoryMetadata();
            if (metadata == null
                || (string.IsNullOrEmpty(metadata.Type)
                    && string.IsNullOrEmpty(metadata.Url)
                    && string.IsNullOrEmpty(metadata.Branch)
                    && string.IsNullOrEmpty(metadata.Commit)))
            {
                return;
            }

            record.RepositoryMetadata = KustoDynamicSerializer.Serialize(metadata);
        }

        private static void ReadContentFiles(NuspecReader nuspecReader, PackageManifestRecord record)
        {
            try
            {
                record.ContentFiles = KustoDynamicSerializer.Serialize(nuspecReader.GetContentFiles().ToList());
            }
            catch (PackagingException ex) when (ex.Message.Contains("The nuspec contains an invalid entry", StringComparison.Ordinal))
            {
                record.ContentFilesHasFormatException = true;
                record.ResultType = PackageManifestRecordResultType.Error;
            }
        }

        private static void ReadDependencyGroups(NuspecReader nuspecReader, PackageManifestRecord record)
        {
            try
            {
                record.DependencyGroups = KustoDynamicSerializer.Serialize(nuspecReader.GetDependencyGroups().ToList());
            }
            catch (ArgumentException ex) when (ex.Message.Contains("The argument cannot be null or empty.", StringComparison.Ordinal) && ex.ParamName == "id")
            {
                record.DependencyGroupsHasMissingId = true;
                record.ResultType = PackageManifestRecordResultType.Error;
            }
        }
    }
}
