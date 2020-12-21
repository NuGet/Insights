using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssets
{
    public partial class PackageAsset : PackageRecord, IEquatable<PackageAsset>, ICsvRecord
    {
        public PackageAsset()
        {
        }

        public PackageAsset(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageAssetResultType.Deleted;
        }

        public PackageAsset(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageAssetResultType resultType)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = resultType;
        }

        public PackageAssetResultType ResultType { get; set; }

        public string PatternSet { get; set; }
        public string PropertyAnyValue { get; set; }
        public string PropertyCodeLanguage { get; set; }
        public string PropertyTargetFrameworkMoniker { get; set; }
        public string PropertyLocale { get; set; }
        public string PropertyManagedAssembly { get; set; }
        public string PropertyMSBuild { get; set; }
        public string PropertyRuntimeIdentifier { get; set; }
        public string PropertySatelliteAssembly { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public string RoundTripTargetFrameworkMoniker { get; set; }
        public string FrameworkName { get; set; }
        public string FrameworkVersion { get; set; }
        public string FrameworkProfile { get; set; }
        public string PlatformName { get; set; }
        public string PlatformVersion { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageAsset);
        }

        public bool Equals([AllowNull] PackageAsset other)
        {
            return other != null &&
                   EqualityComparer<Guid?>.Default.Equals(ScanId, other.ScanId) &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(ScanTimestamp, other.ScanTimestamp) &&
                   Id == other.Id &&
                   Version == other.Version &&
                   CatalogCommitTimestamp.Equals(other.CatalogCommitTimestamp) &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(Created, other.Created) &&
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
                   Path == other.Path &&
                   FileName == other.FileName &&
                   FileExtension == other.FileExtension &&
                   TopLevelFolder == other.TopLevelFolder &&
                   RoundTripTargetFrameworkMoniker == other.RoundTripTargetFrameworkMoniker &&
                   FrameworkName == other.FrameworkName &&
                   FrameworkVersion == other.FrameworkVersion &&
                   FrameworkProfile == other.FrameworkProfile &&
                   PlatformName == other.PlatformName &&
                   PlatformVersion == other.PlatformVersion;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(ScanId);
            hash.Add(ScanTimestamp);
            hash.Add(Id);
            hash.Add(Version);
            hash.Add(CatalogCommitTimestamp);
            hash.Add(Created);
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
            hash.Add(FileName);
            hash.Add(FileExtension);
            hash.Add(TopLevelFolder);
            hash.Add(RoundTripTargetFrameworkMoniker);
            hash.Add(FrameworkName);
            hash.Add(FrameworkVersion);
            hash.Add(FrameworkProfile);
            hash.Add(PlatformName);
            hash.Add(PlatformVersion);
            return hash.ToHashCode();
        }
    }
}
