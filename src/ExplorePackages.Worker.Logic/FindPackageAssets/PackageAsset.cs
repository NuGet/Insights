using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;

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

        public bool TryRead(TextReader reader, List<string> fields, StringBuilder builder)
        {
            if (!CsvUtility.TryReadLine(reader, fields, builder))
            {
                return false;
            }

            ScanId = ParseNullableGuid(fields[0]);
            ScanTimestamp = ParseNullableDateTimeOffset(fields[1]);
            Id = fields[2];
            Version = fields[3];
            Created = ParseDateTimeOffset(fields[4]);
            ResultType = fields[5];
            PatternSet = fields[6];
            PropertyAnyValue = fields[7];
            PropertyCodeLanguage = fields[8];
            PropertyTargetFrameworkMoniker = fields[9];
            PropertyLocale = fields[10];
            PropertyManagedAssembly = fields[11];
            PropertyMSBuild = fields[12];
            PropertyRuntimeIdentifier = fields[13];
            PropertySatelliteAssembly = fields[14];
            Path = fields[15];
            FileName = fields[16];
            FileExtension = fields[17];
            TopLevelFolder = fields[18];
            RoundTripTargetFrameworkMoniker = fields[19];
            FrameworkName = fields[20];
            FrameworkVersion = fields[21];
            FrameworkProfile = fields[22];
            PlatformName = fields[23];
            PlatformVersion = fields[24];

            return true;
        }

        public bool TryRead(NReco.Csv.CsvReader reader)
        {
            if (!reader.Read())
            {
                return false;
            }

            ScanId = ParseNullableGuid(reader[0]);
            ScanTimestamp = ParseNullableDateTimeOffset(reader[1]);
            Id = reader[2];
            Version = reader[3];
            Created = ParseDateTimeOffset(reader[4]);
            ResultType = reader[5];
            PatternSet = reader[6];
            PropertyAnyValue = reader[7];
            PropertyCodeLanguage = reader[8];
            PropertyTargetFrameworkMoniker = reader[9];
            PropertyLocale = reader[10];
            PropertyManagedAssembly = reader[11];
            PropertyMSBuild = reader[12];
            PropertyRuntimeIdentifier = reader[13];
            PropertySatelliteAssembly = reader[14];
            Path = reader[15];
            FileName = reader[16];
            FileExtension = reader[17];
            TopLevelFolder = reader[18];
            RoundTripTargetFrameworkMoniker = reader[19];
            FrameworkName = reader[20];
            FrameworkVersion = reader[21];
            FrameworkProfile = reader[22];
            PlatformName = reader[23];
            PlatformVersion = reader[24];

            return true;
        }

        public void Write(NReco.Csv.CsvWriter writer)
        {
            writer.WriteField(ScanId?.ToString());
            writer.WriteField(ScanTimestamp?.ToString("O"));
            writer.WriteField(Id);
            writer.WriteField(Version);
            writer.WriteField(Created.ToString("O"));
            writer.WriteField(ResultType);
            writer.WriteField(PatternSet);
            writer.WriteField(PropertyAnyValue);
            writer.WriteField(PropertyCodeLanguage);
            writer.WriteField(PropertyTargetFrameworkMoniker);
            writer.WriteField(PropertyLocale);
            writer.WriteField(PropertyManagedAssembly);
            writer.WriteField(PropertyMSBuild);
            writer.WriteField(PropertyRuntimeIdentifier);
            writer.WriteField(PropertySatelliteAssembly);
            writer.WriteField(Path);
            writer.WriteField(FileName);
            writer.WriteField(FileExtension);
            writer.WriteField(TopLevelFolder);
            writer.WriteField(RoundTripTargetFrameworkMoniker);
            writer.WriteField(FrameworkName);
            writer.WriteField(FrameworkVersion);
            writer.WriteField(FrameworkProfile);
            writer.WriteField(PlatformName);
            writer.WriteField(PlatformVersion);
            writer.NextRecord();
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
    }
}
