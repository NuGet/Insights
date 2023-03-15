// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

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
                PackageTypes = JsonSerialize(nuspecReader.GetPackageTypes()),
                OriginalVersion = nuspecReader.GetVersion().OriginalVersion,
                IsServiceable = nuspecReader.IsServiceable(),

                // From NuspecReader
                Authors = nuspecReader.GetAuthors(),
                Copyright = nuspecReader.GetCopyright(),
                Description = nuspecReader.GetDescription(),
                FrameworkAssemblyGroups = JsonSerialize(nuspecReader.GetFrameworkAssemblyGroups()),
                FrameworkRefGroups = JsonSerialize(nuspecReader.GetFrameworkRefGroups()),
                Icon = nuspecReader.GetIcon(),
                IconUrl = nuspecReader.GetIconUrl(),
                Language = nuspecReader.GetLanguage(),
                LicenseUrl = nuspecReader.GetLicenseUrl(),
                Owners = nuspecReader.GetOwners(),
                ProjectUrl = nuspecReader.GetProjectUrl(),
                Readme = nuspecReader.GetReadme(),
                ReferenceGroups = JsonSerialize(nuspecReader.GetReferenceGroups()),
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
            record.SplitTags = JsonSerialize(splitTags);
        }

        private static void ReadLicenseMetadata(NuspecReader nuspecReader, PackageManifestRecord record)
        {
            var metadata = nuspecReader.GetLicenseMetadata();
            if (metadata == null)
            {
                return;
            }

            record.LicenseMetadata = JsonSerialize(metadata);
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

            record.RepositoryMetadata = JsonSerialize(metadata);
        }

        private static void ReadContentFiles(NuspecReader nuspecReader, PackageManifestRecord record)
        {
            try
            {
                record.ContentFiles = JsonSerialize(nuspecReader.GetContentFiles());
            }
            catch (PackagingException ex) when (ex.Message.Contains("The nuspec contains an invalid entry"))
            {
                record.ContentFilesHasFormatException = true;
                record.ResultType = PackageManifestRecordResultType.Error;
            }
        }

        private static void ReadDependencyGroups(NuspecReader nuspecReader, PackageManifestRecord record)
        {
            try
            {
                record.DependencyGroups = JsonSerialize(nuspecReader.GetDependencyGroups());
            }
            catch (ArgumentException ex) when (ex.Message.Contains("The argument cannot be null or empty.") && ex.ParamName == "id")
            {
                record.DependencyGroupsHasMissingId = true;
                record.ResultType = PackageManifestRecordResultType.Error;
            }
        }

        private static string JsonSerialize(object input)
        {
            return JsonSerializer.Serialize(input, JsonSerializerOptions);
        }

        public Task<(ICatalogLeafItem LeafItem, string PageUrl)> MakeReprocessItemOrNullAsync(PackageManifestRecord record)
        {
            throw new NotImplementedException();
        }

        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(),
                new NuGetFrameworkJsonConverter(),
                new VersionRangeJsonConverter(),
                new LicenseMetadataJsonConverter(),
                new FrameworkSpecificGroupJsonConverter(),
            },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private class NuGetFrameworkJsonConverter : JsonConverter<NuGetFramework>
        {
            public override NuGetFramework Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, NuGetFramework value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.GetShortFolderName());
            }
        }

        private class VersionRangeJsonConverter : JsonConverter<VersionRange>
        {
            public override VersionRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, VersionRange value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToNormalizedString());
            }
        }

        private class LicenseMetadataJsonConverter : JsonConverter<LicenseMetadata>
        {
            public override LicenseMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, LicenseMetadata value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, new
                {
                    value.Type,
                    value.License,
                    value.WarningsAndErrors,
                    value.Version,
                    value.LicenseUrl,
                }, options);
            }
        }

        private class FrameworkSpecificGroupJsonConverter : JsonConverter<FrameworkSpecificGroup>
        {
            public override FrameworkSpecificGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, FrameworkSpecificGroup value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, new
                {
                    value.TargetFramework,
                    value.Items,
                }, options);
            }
        }
    }
}
