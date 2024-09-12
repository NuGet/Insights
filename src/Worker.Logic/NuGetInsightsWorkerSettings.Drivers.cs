// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings
    {
        public string VersionSetAggregateTableName { get; set; } = "versionset";
        public string VersionSetContainerName { get; set; } = "versionset";

        public string BucketedPackageTableName { get; set; } = "bucketedpackages";

        public string LatestPackageLeafTableName { get; set; } = "latestpackageleaves";

        public string PackageAssetContainerName { get; set; } = "packageassets";

        public string PackageAssemblyContainerName { get; set; } = "packageassemblies";

        public string PackageManifestContainerName { get; set; } = "packagemanifests";

        public string PackageReadmeContainerName { get; set; } = "packagereadmes";

        public string PackageLicenseContainerName { get; set; } = "packagelicenses";

        public string PackageSignatureContainerName { get; set; } = "packagesignatures";

        public string PackageArchiveContainerName { get; set; } = "packagearchives";
        public string PackageArchiveEntryContainerName { get; set; } = "packagearchiveentries";

        public string SymbolPackageArchiveContainerName { get; set; } = "symbolpackagearchives";
        public string SymbolPackageArchiveEntryContainerName { get; set; } = "symbolpackagearchiveentries";

        public string PackageFileContainerName { get; set; } = "packagefiles";

        public string SymbolPackageFileContainerName { get; set; } = "symbolpackagefiles";

        public string PackageVersionTableName { get; set; } = "packageversions";

        public string PackageVersionContainerName { get; set; } = "packageversions";

        public string CatalogLeafItemContainerName { get; set; } = "catalogleafitems";
        public string PackageDeprecationContainerName { get; set; } = "packagedeprecations";
        public string PackageVulnerabilityContainerName { get; set; } = "packagevulnerabilities";

        public string PackageIconContainerName { get; set; } = "packageicons";

        public string PackageCompatibilityContainerName { get; set; } = "packagecompatibilities";

        /// <summary>
        /// The types of package content to index in <see cref="PackageContentToCsv.PackageContentToCsvDriver"/>. The
        /// order of this list is significant (entries are processed in the matching order). The values are treated as
        /// file path suffixes so be sure to include a dot (".") if you want file extension behavior.
        /// </summary>
        public List<string> PackageContentFileExtensions { get; set; } = new List<string>();
        public int PackageContentMaxSizePerPackage { get; set; } = 1024 * 16;
        public int PackageContentMaxSizePerFile { get; set; } = 1024 * 16;
        public string PackageContentContainerName { get; set; } = "packagecontents";
    }
}
