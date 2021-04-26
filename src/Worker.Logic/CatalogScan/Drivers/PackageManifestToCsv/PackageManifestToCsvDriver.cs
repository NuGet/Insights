using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.PackageManifestToCsv
{
    public class PackageManifestToCsvDriver : ICatalogLeafToCsvDriver<PackageManifestRecord>, ICsvResultStorage<PackageManifestRecord>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageManifestService _packageManifestService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public PackageManifestToCsvDriver(
            CatalogClient catalogClient,
            PackageManifestService packageManifestService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _packageManifestService = packageManifestService;
            _options = options;
        }

        public string ResultContainerName => _options.Value.PackageManifestContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageManifestRecord> Prune(List<PackageManifestRecord> records)
        {
            return PackageRecord.Prune(records);
        }

        public async Task InitializeAsync()
        {
            await _packageManifestService.InitializeAsync();
        }
        public async Task<DriverResult<CsvRecordSet<PackageManifestRecord>>> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
        {
            var records = await ProcessLeafInternalAsync(item);
            return DriverResult.Success(new CsvRecordSet<PackageManifestRecord>(PackageRecord.GetBucketKey(item), records));
        }

        private async Task<List<PackageManifestRecord>> ProcessLeafInternalAsync(CatalogLeafItem item)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                return new List<PackageManifestRecord> { new PackageManifestRecord(scanId, scanTimestamp, leaf) };
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                (var nuspecReader, var size) = await _packageManifestService.GetNuspecReaderAndSizeAsync(item);
                if (nuspecReader == null)
                {
                    // Ignore packages where the .nuspec is missing. A subsequent scan will produce a deleted asset record.
                    return new List<PackageManifestRecord>();
                }

                return new List<PackageManifestRecord> { GetRecord(scanId, scanTimestamp, leaf, nuspecReader, size) };
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

            return record;
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
            catch (FormatException ex) when (ex.Message.Contains("Index (zero based) must be greater than or equal to zero and less than the size of the argument list."))
            {
                // See: https://github.com/NuGet/NuGet.Client/pull/3914
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
            return JsonConvert.SerializeObject(input, JsonSerializerSettings);
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(PackageManifestRecord record)
        {
            throw new NotImplementedException();
        }

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            Converters =
            {
                new StringEnumConverter(),
                new NuGetFrameworkJsonConverter(),
                new VersionRangeJsonConverter(),
            },
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new PropertyRenameAndIgnoreSerializerContractResolver()
                .IgnoreProperty(typeof(LicenseMetadata), nameof(LicenseMetadata.LicenseExpression))
                .IgnoreProperty(typeof(FrameworkSpecificGroup), nameof(FrameworkSpecificGroup.HasEmptyFolder))
        };

        private class NuGetFrameworkJsonConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(NuGetFramework);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(((NuGetFramework)value).GetShortFolderName());
            }
        }

        private class VersionRangeJsonConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(VersionRange);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(((VersionRange)value).ToNormalizedString());
            }
        }

        /// <summary>
        /// Source: https://blog.rsuter.com/advanced-newtonsoft-json-dynamically-rename-or-ignore-properties-without-changing-the-serialized-class/
        /// </summary>
        public class PropertyRenameAndIgnoreSerializerContractResolver : DefaultContractResolver
        {
            private readonly Dictionary<Type, HashSet<string>> _ignores;
            private readonly Dictionary<Type, Dictionary<string, string>> _renames;

            public PropertyRenameAndIgnoreSerializerContractResolver()
            {
                _ignores = new Dictionary<Type, HashSet<string>>();
                _renames = new Dictionary<Type, Dictionary<string, string>>();
            }

            public PropertyRenameAndIgnoreSerializerContractResolver IgnoreProperty(Type type, params string[] jsonPropertyNames)
            {
                if (!_ignores.ContainsKey(type))
                {
                    _ignores[type] = new HashSet<string>();
                }

                foreach (var prop in jsonPropertyNames)
                {
                    _ignores[type].Add(prop);
                }

                return this;
            }

            public PropertyRenameAndIgnoreSerializerContractResolver RenameProperty(Type type, string propertyName, string newJsonPropertyName)
            {
                if (!_renames.ContainsKey(type))
                {
                    _renames[type] = new Dictionary<string, string>();
                }

                _renames[type][propertyName] = newJsonPropertyName;

                return this;
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                if (IsIgnored(property.DeclaringType, property.PropertyName))
                {
                    property.ShouldSerialize = i => false;
                    property.Ignored = true;
                }

                if (IsRenamed(property.DeclaringType, property.PropertyName, out var newJsonPropertyName))
                {
                    property.PropertyName = newJsonPropertyName;
                }

                return property;
            }

            private bool IsIgnored(Type type, string jsonPropertyName)
            {
                if (!_ignores.ContainsKey(type))
                {
                    return false;
                }

                return _ignores[type].Contains(jsonPropertyName);
            }

            private bool IsRenamed(Type type, string jsonPropertyName, out string newJsonPropertyName)
            {

                if (!_renames.TryGetValue(type, out var renames) || !renames.TryGetValue(jsonPropertyName, out newJsonPropertyName))
                {
                    newJsonPropertyName = null;
                    return false;
                }

                return true;
            }
        }
    }
}
