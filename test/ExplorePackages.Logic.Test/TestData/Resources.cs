using System.Xml.Linq;
using Knapcode.ExplorePackages.Logic;

namespace Knapcode.ExplorePackages.Logic.TestData
{
    public static class Resources
    {
        public static class Nuspecs
        {
            public const string CollidingMetadataElements = "CollidingMetadataElements.nuspec";
            public const string DependencyGroups = NuGet_Versioning_4_3_0;
            public const string DuplicateDependencies = "DuplicateDependencies.nuspec";
            public const string DuplicateDependencyTargetFrameworks = "DuplicateDependencyTargetFrameworks.nuspec";
            public const string DuplicateMetadataElements = "DuplicateMetadataElements.nuspec";
            public const string FloatingDependencyVersions = "FloatingDependencyVersions.nuspec";
            public const string InvalidDependencyIds = "InvalidDependencyIds.nuspec";
            public const string InvalidDependencyTargetFrameworks = "InvalidDependencyTargetFrameworks.nuspec";
            public const string InvalidDependencyVersions = "InvalidDependencyVersions.nuspec";
            public const string LegacyDependencies = NuGet_Core_2_14_0;
            public const string MixedDependencyGroupStyles = "MixedDependencyGroupStyles.nuspec";
            public const string NoDependencies = NuGet_Versioning_1_0_0;
            public const string NonAlphabetMetadataElements = "NonAlphabetMetadataElements.nuspec";
            public const string UnexpectedValuesForBooleans = "UnexpectedValuesForBooleans.nuspec";
            public const string UnsupportedDependencyTargetFrameworks = "UnsupportedDependencyTargetFrameworks.nuspec";
            public const string WhitespaceDependencyTargetFrameworks = "WhitespaceDependencyTargetFrameworks.nuspec";

            public const string Microsoft_AspNetCore_1_1_2 = "Microsoft.AspNetCore.1.1.2.nuspec";
            public const string Newtonsoft_Json_10_0_3 = "Newtonsoft.Json.10.0.3.nuspec";
            public const string NuGet_Core_2_14_0 = "NuGet.Core.2.14.0.nuspec";
            public const string NuGet_Versioning_1_0_0 = "NuGet.Versioning.1.0.0.nuspec";
            public const string NuGet_Versioning_4_3_0 = "NuGet.Versioning.4.3.0.nuspec";
        }

        public static XDocument LoadXml(string resourceName)
        {
            var resourceStream = typeof(Resources)
                .Assembly
                .GetManifestResourceStream(typeof(Resources).Namespace + "."  + resourceName);

            if (resourceName == null)
            {
                return null;
            }

            using (resourceStream)
            {
                return XmlUtility.LoadXml(resourceStream);
            }
        }
    }
}
