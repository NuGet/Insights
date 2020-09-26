using System;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class PackageAsset
    {
        public PackageAsset(PackageDetailsCatalogLeaf leaf, string packageVersion, string resultType)
        {
            Id = leaf.PackageId;
            Version = packageVersion;
            Created = leaf.Created;
            ResultType = resultType;
        }

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
    }
}
