// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings
    {
        public string VersionSetAggregateTableNamePrefix { get; set; } = "versionset";
        public BlobContainerStorageSettings VersionSetContainer { get; set; } = "versionset";

        public TableStorageSettings BucketedPackageTable { get; set; } = "bucketedpackages";

        public TableStorageSettings LatestPackageLeafTable { get; set; } = "latestpackageleaves";

        public BlobContainerStorageSettings PackageAssetContainer { get; set; } = "packageassets";

        public BlobContainerStorageSettings PackageAssemblyContainer { get; set; } = "packageassemblies";

        public BlobContainerStorageSettings PackageManifestContainer { get; set; } = "packagemanifests";

        public BlobContainerStorageSettings PackageReadmeContainer { get; set; } = "packagereadmes";

        public BlobContainerStorageSettings PackageLicenseContainer { get; set; } = "packagelicenses";

        public bool RecordCertificateStatus { get; set; } = true;

        public BlobContainerStorageSettings PackageSignatureContainer { get; set; } = "packagesignatures";

        /// <summary>
        /// Don't set the Content-MD5 header in output CSVs (e.g. for <see cref="PackageArchiveToCsv"/>). The header
        /// appears to be returned inconsistently from some CDN endpoints leading to test flakiness.
        /// </summary>
        public bool SkipContentMD5HeaderInCsv { get; set; } = false;

        public BlobContainerStorageSettings PackageArchiveContainer { get; set; } = "packagearchives";
        public BlobContainerStorageSettings PackageArchiveEntryContainer { get; set; } = "packagearchiveentries";

        public BlobContainerStorageSettings SymbolPackageArchiveContainer { get; set; } = "symbolpackagearchives";
        public BlobContainerStorageSettings SymbolPackageArchiveEntryContainer { get; set; } = "symbolpackagearchiveentries";

        public BlobContainerStorageSettings PackageFileContainer { get; set; } = "packagefiles";

        public BlobContainerStorageSettings SymbolPackageFileContainer { get; set; } = "symbolpackagefiles";

        public TableStorageSettings PackageVersionTable { get; set; } = "packageversions";

        public BlobContainerStorageSettings PackageVersionContainer { get; set; } = "packageversions";

        public BlobContainerStorageSettings CatalogLeafItemContainer { get; set; } = "catalogleafitems";
        public BlobContainerStorageSettings PackageDeprecationContainer { get; set; } = "packagedeprecations";
        public BlobContainerStorageSettings PackageVulnerabilityContainer { get; set; } = "packagevulnerabilities";

        public BlobContainerStorageSettings PackageIconContainer { get; set; } = "packageicons";

        public BlobContainerStorageSettings PackageCompatibilityContainer { get; set; } = "packagecompatibilities";

        /// <summary>
        /// The types of package content to index in <see cref="PackageContentToCsv.PackageContentToCsvDriver"/>. The
        /// order of this list is significant (entries are processed in the matching order). The values are treated as
        /// file path suffixes so be sure to include a dot (".") if you want file extension behavior.
        /// </summary>
        public List<string> PackageContentFileExtensions { get; set; } = new List<string>();
        public int PackageContentMaxSizePerPackage { get; set; } = 1024 * 16;
        public int PackageContentMaxSizePerFile { get; set; } = 1024 * 16;
        public BlobContainerStorageSettings PackageContentContainer { get; set; } = "packagecontents";
    }
}
