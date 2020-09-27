using CsvHelper.Configuration.Attributes;
using NuGet.Versioning;
using System;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class PackageAsset : IEquatable<PackageAsset>
    {
        public PackageAsset()
        {
        }

        public PackageAsset(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, string resultType)
        {
            ScanId = scanId;
            ScanTimestamp = scanTimestamp;
            Id = leaf.PackageId;
            Version = NuGetVersion.Parse(leaf.PackageVersion).ToNormalizedString();
            Created = leaf.Created;
            ResultType = resultType;
        }

        [Index(0)] public Guid ScanId { get; set; }
        [Index(1)] public DateTimeOffset ScanTimestamp { get; set; }
        [Index(2)] public string Id { get; set; }
        [Index(3)] public string Version { get; set; }
        [Index(4)] public DateTimeOffset Created { get; set; }
        [Index(5)] public string ResultType { get; set; }

        [Index(6)] public string PatternSet { get; set; }
        [Index(7)] public string PropertyAnyValue { get; set; }
        [Index(8)] public string PropertyCodeLanguage { get; set; }
        [Index(9)] public string PropertyTargetFrameworkMoniker { get; set; }
        [Index(10)] public string PropertyLocale { get; set; }
        [Index(11)] public string PropertyManagedAssembly { get; set; }
        [Index(12)] public string PropertyMSBuild { get; set; }
        [Index(13)] public string PropertyRuntimeIdentifier { get; set; }
        [Index(14)] public string PropertySatelliteAssembly { get; set; }

        [Index(15)] public string Path { get; set; }
        [Index(16)] public string FileName { get; set; }
        [Index(17)] public string FileExtension { get; set; }
        [Index(18)] public string TopLevelFolder { get; set; }

        [Index(19)] public string RoundTripTargetFrameworkMoniker { get; set; }
        [Index(20)] public string FrameworkName { get; set; }
        [Index(21)] public string FrameworkVersion { get; set; }
        [Index(22)] public string FrameworkProfile { get; set; }
        [Index(23)] public string PlatformName { get; set; }
        [Index(24)] public string PlatformVersion { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageAsset);
        }

        public bool Equals(PackageAsset other)
        {
            return other != null &&
                   Id == other.Id &&
                   Version == other.Version &&
                   ResultType == other.ResultType &&
                   PatternSet == other.PatternSet &&
                   PropertyAnyValue == other.PropertyAnyValue &&
                   PropertyCodeLanguage == other.PropertyCodeLanguage &&
                   PropertyTargetFrameworkMoniker == other.PropertyTargetFrameworkMoniker &&
                   PropertyLocale == other.PropertyLocale &&
                   PropertyManagedAssembly == other.PropertyManagedAssembly &&
                   PropertyMSBuild == other.PropertyMSBuild &&
                   PropertyRuntimeIdentifier == other.PropertyRuntimeIdentifier &&
                   PropertySatelliteAssembly == other.PropertySatelliteAssembly &&
                   Path == other.Path;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(Id);
            hash.Add(Version);
            hash.Add(ResultType);
            hash.Add(PatternSet);
            hash.Add(PropertyAnyValue);
            hash.Add(PropertyCodeLanguage);
            hash.Add(PropertyTargetFrameworkMoniker);
            hash.Add(PropertyLocale);
            hash.Add(PropertyManagedAssembly);
            hash.Add(PropertyMSBuild);
            hash.Add(PropertyRuntimeIdentifier);
            hash.Add(PropertySatelliteAssembly);
            hash.Add(Path);
            return hash.ToHashCode();
        }
    }
}
