using NuGet.Versioning;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssets
{
    public partial class PackageAsset : IEquatable<PackageAsset>, ICsvWritable
    {
        public PackageAsset()
        {
        }

        public PackageAsset(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf, string resultType)
        {
            ScanId = scanId;
            ScanTimestamp = scanTimestamp;
            Id = leaf.PackageId;
            Version = NuGetVersion.Parse(leaf.PackageVersion).ToNormalizedString();
            Created = leaf.Created;
            ResultType = resultType;
        }

        public Guid? ScanId { get; set; }
        public DateTimeOffset? ScanTimestamp { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public DateTimeOffset Created { get; set; }
        public string ResultType { get; set; }

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
                   ScanId.Equals(other.ScanId) &&
                   ScanTimestamp.Equals(other.ScanTimestamp) &&
                   Id == other.Id &&
                   Version == other.Version &&
                   Created.Equals(other.Created) &&
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

        private static Guid? ParseNullableGuid(string input)
        {
            return input.Length > 0 ? Guid.Parse(input) : (Guid?)null;
        }

        private static DateTimeOffset? ParseNullableDateTimeOffset(string input)
        {
            return input.Length > 0 ? ParseDateTimeOffset(input) : (DateTimeOffset?)null;
        }

        private static DateTimeOffset ParseDateTimeOffset(string input)
        {
            return DateTimeOffset.ParseExact(input, "O", CultureInfo.InvariantCulture);
        }

        public void Read(Func<int, string> getField)
        {
            ScanId = ParseNullableGuid(getField(0));
            ScanTimestamp = ParseNullableDateTimeOffset(getField(1));
            Id = getField(2);
            Version = getField(3);
            Created = ParseDateTimeOffset(getField(4));
            ResultType = getField(5);
            PatternSet = getField(6);
            PropertyAnyValue = getField(7);
            PropertyCodeLanguage = getField(8);
            PropertyTargetFrameworkMoniker = getField(9);
            PropertyLocale = getField(10);
            PropertyManagedAssembly = getField(11);
            PropertyMSBuild = getField(12);
            PropertyRuntimeIdentifier = getField(13);
            PropertySatelliteAssembly = getField(14);
            Path = getField(15);
            FileName = getField(16);
            FileExtension = getField(17);
            TopLevelFolder = getField(18);
            RoundTripTargetFrameworkMoniker = getField(19);
            FrameworkName = getField(20);
            FrameworkVersion = getField(21);
            FrameworkProfile = getField(22);
            PlatformName = getField(23);
            PlatformVersion = getField(24);
        }
    }
}
