using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class PackageDetailsCatalogLeaf : CatalogLeaf
    {
        [JsonProperty("authors")]
        public string Authors { get; set; }

        [JsonProperty("created")]
        public DateTimeOffset Created { get; set; }

        [JsonProperty("lastEdited")]
        public DateTimeOffset LastEdited { get; set; }

        [JsonProperty("dependencyGroups")]
        public List<CatalogPackageDependencyGroup> DependencyGroups { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; }

        [JsonProperty("isPrerelease")]
        public bool IsPrerelease { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("licenseUrl")]
        public string LicenseUrl { get; set; }

        [JsonProperty("listed")]
        public bool? Listed { get; set; }

        [JsonProperty("minClientVersion")]
        public string MinClientVersion { get; set; }

        [JsonProperty("packageHash")]
        public string PackageHash { get; set; }

        [JsonProperty("packageHashAlgorithm")]
        public string PackageHashAlgorithm { get; set; }

        [JsonProperty("packageSize")]
        public long PackageSize { get; set; }

        [JsonProperty("projectUrl")]
        public string ProjectUrl { get; set; }

        [JsonProperty("releaseNotes")]
        [JsonConverter(typeof(FirstStringConverter))] // https://api.nuget.org/v3/catalog0/data/2018.03.11.05.06.09/fluentconsoleapplication.0.1.0.json
        public string ReleaseNotes { get; set; }

        [JsonProperty("requireLicenseAgreement")]
        public bool? RequireLicenseAgreement { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("verbatimVersion")]
        public string VerbatimVersion { get; set; }
    }
}
