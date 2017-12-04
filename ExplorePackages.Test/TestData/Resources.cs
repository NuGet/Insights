using System.Xml.Linq;
using Knapcode.ExplorePackages.Logic;

namespace Knapcode.ExplorePackages.TestData
{
    public static class Resources
    {
        public static class Nuspecs
        {
            public const string DuplicateDependencyTargetFrameworks = "DuplicateDependencyTargetFrameworks.nuspec";
            public const string InvalidDependencyIds = "InvalidDependencyIds.nuspec";
            public const string InvalidDependencyTargetFrameworks = "InvalidDependencyTargetFrameworks.nuspec";
            public const string InvalidDependencyVersions = "InvalidDependencyVersions.nuspec";
            public const string MixedDependencyGroupStyles = "MixedDependencyGroupStyles.nuspec";
            public const string NuGet_Core_2_14_0 = "NuGet.Core.2.14.0.nuspec";
            public const string NuGet_Versioning_1_0_0 = "NuGet.Versioning.1.0.0.nuspec";
            public const string NuGet_Versioning_4_3_0 = "NuGet.Versioning.4.3.0.nuspec";
            public const string UnsupportedDependencyTargetFrameworks = "UnsupportedDependencyTargetFrameworks.nuspec";
            public const string WhitespaceDependencyTargetFrameworks = "WhitespaceDependencyTargetFrameworks.nuspec";

            public const string LegacyDependencies = NuGet_Core_2_14_0;
            public const string DependencyGroups = NuGet_Versioning_4_3_0;
            public const string NoDependencies = NuGet_Versioning_1_0_0;
        }

        public static XDocument LoadXml(string resourceName)
        {
            var resourceStream = typeof(Resources)
                .Assembly
                .GetManifestResourceStream("Knapcode.ExplorePackages.TestData." + resourceName);

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
