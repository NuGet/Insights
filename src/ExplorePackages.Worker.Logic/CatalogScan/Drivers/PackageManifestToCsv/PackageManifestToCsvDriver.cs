using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.PackageManifestToCsv
{
    public class PackageManifestToCsvDriver : ICatalogLeafToCsvDriver<PackageManifestRecord>
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

        public string ResultsContainerName => _options.Value.PackageManifestContainerName;

        public List<PackageManifestRecord> Prune(List<PackageManifestRecord> records)
        {
            return PackageRecord.Prune(records);
        }

        public async Task InitializeAsync()
        {
            await _packageManifestService.InitializeAsync();
        }

        public async Task<DriverResult<List<PackageManifestRecord>>> ProcessLeafAsync(CatalogLeafItem item)
        {
            Guid? scanId = null;
            DateTimeOffset? scanTimestamp = null;
            if (_options.Value.AppendResultUniqueIds)
            {
                scanId = Guid.NewGuid();
                scanTimestamp = DateTimeOffset.UtcNow;
            }

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                return DriverResult.Success(new List<PackageManifestRecord> { new PackageManifestRecord(scanId, scanTimestamp, leaf) });
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                var manifest = await _packageManifestService.GetManifestAsync(item);
                if (manifest == null)
                {
                    // Ignore packages where the .nuspec is missing. A subsequent scan will produce a deleted asset record.
                    return DriverResult.Success(new List<PackageManifestRecord>());
                }

                return DriverResult.Success(new List<PackageManifestRecord> { GetRecord(scanId, scanTimestamp, leaf, manifest) });
            }
        }

        private PackageManifestRecord GetRecord(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf, XDocument manifest)
        {
            var nuspecReader = new NuspecReader(manifest);

            return new PackageManifestRecord(scanId, scanTimestamp, leaf)
            {
                // From NuspecCoreReaderBase
                DevelopmentDependency = nuspecReader.GetDevelopmentDependency(),
                OriginalId = nuspecReader.GetId(),
                MinClientVersion = nuspecReader.GetMinClientVersion()?.ToNormalizedString(),
                PackageTypes = JsonSerialize(nuspecReader.GetPackageTypes()),
                OriginalVersion = nuspecReader.GetVersion().OriginalVersion,
                IsServiceable = nuspecReader.IsServiceable(),

                // From NuspecReader
                Authors = nuspecReader.GetAuthors(),
                ContentFiles = JsonSerialize(nuspecReader.GetContentFiles()),
                Copyright = nuspecReader.GetCopyright(),
                DependencyGroups = JsonSerialize(nuspecReader.GetDependencyGroups()),
                Description = nuspecReader.GetDescription(),
                FrameworkAssemblyGroups = JsonSerialize(nuspecReader.GetFrameworkAssemblyGroups()),
                FrameworkRefGroups = JsonSerialize(nuspecReader.GetFrameworkRefGroups()),
                Icon = nuspecReader.GetIcon(),
                IconUrl = nuspecReader.GetIconUrl(),
                Language = nuspecReader.GetLanguage(),
                LicenseMetadata = JsonSerialize(nuspecReader.GetLicenseMetadata()),
                LicenseUrl = nuspecReader.GetLicenseUrl(),
                Owners = nuspecReader.GetOwners(),
                ProjectUrl = nuspecReader.GetProjectUrl(),
                ReferenceGroups = JsonSerialize(nuspecReader.GetReferenceGroups()),
                ReleaseNotes = nuspecReader.GetReleaseNotes(),
                RepositoryMetadata = JsonSerialize(nuspecReader.GetRepositoryMetadata()),
                RequireLicenseAcceptance = nuspecReader.GetRequireLicenseAcceptance(),
                Summary = nuspecReader.GetSummary(),
                Tags = nuspecReader.GetTags(),
                Title = nuspecReader.GetTitle(),
            };
        }

        private static string JsonSerialize(object input)
        {
            return JsonConvert.SerializeObject(input, JsonSerializerSettings);
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
