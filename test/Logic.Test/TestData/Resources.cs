using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace NuGet.Insights
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

        public static MemoryStream LoadMemoryStream(string resourceName)
        {
            using (var fileStream = GetFileStream(resourceName))
            {
                var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                return memoryStream;
            }
        }

        public static StringReader LoadStringReader(string resourceName)
        {
            using (var reader = new StreamReader(GetFileStream(resourceName)))
            {
                return new StringReader(reader.ReadToEnd());
            }
        }

        public static T LoadJson<T>(string resourceName)
        {
            using (var reader = new StreamReader(GetFileStream(resourceName)))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return new JsonSerializer().Deserialize<T>(jsonReader);
            }
        }

        public static XDocument LoadXml(string resourceName)
        {
            using (var stream = LoadMemoryStream(resourceName))
            {
                return XmlUtility.LoadXml(stream);
            }
        }

        private static FileStream GetFileStream(string resourceName)
        {
            return File.OpenRead(Path.Combine("TestData", resourceName));
        }
    }
}
