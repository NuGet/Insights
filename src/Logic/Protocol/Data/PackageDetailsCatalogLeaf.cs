// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageDetailsCatalogLeaf : CatalogLeaf
    {
        [JsonPropertyName("authors")]
        public string Authors { get; set; }

        [JsonPropertyName("created")]
        public DateTimeOffset Created { get; set; }

        [JsonPropertyName("lastEdited")]
        public DateTimeOffset LastEdited { get; set; }

        [JsonPropertyName("dependencyGroups")]
        public List<CatalogPackageDependencyGroup> DependencyGroups { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; }

        [JsonPropertyName("isPrerelease")]
        public bool IsPrerelease { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("licenseExpression")]
        public string LicenseExpression { get; set; }

        [JsonPropertyName("licenseFile")]
        public string LicenseFile { get; set; }

        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; set; }

        [JsonPropertyName("listed")]
        public bool? Listed { get; set; }

        [JsonPropertyName("minClientVersion")]
        public string MinClientVersion { get; set; }

        [JsonPropertyName("packageHash")]
        public string PackageHash { get; set; }

        [JsonPropertyName("packageHashAlgorithm")]
        public string PackageHashAlgorithm { get; set; }

        [JsonPropertyName("packageSize")]
        public long PackageSize { get; set; }

        [JsonPropertyName("projectUrl")]
        public string ProjectUrl { get; set; }

        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes { get; set; }

        [JsonPropertyName("requireLicenseAgreement")]
        public bool? RequireLicenseAgreement { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("verbatimVersion")]
        public string VerbatimVersion { get; set; }

        [JsonPropertyName("deprecation")]
        public PackageDeprecation Deprecation { get; set; }

        [JsonPropertyName("vulnerabilities")]
        public List<PackageVulnerability> Vulnerabilities { get; set; }
    }
}
