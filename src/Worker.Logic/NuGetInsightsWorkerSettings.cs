// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Insights.Worker
{
    public class NuGetInsightsWorkerSettings : NuGetInsightsSettings
    {
        public NuGetInsightsWorkerSettings()
        {
            UseBulkEnqueueStrategy = true;
            BulkEnqueueThreshold = 10;
            EnqueueWorkers = 1;
            AppendResultStorageBucketCount = 1000; // Azure Data Explorer can only import up to 1000 blobs.
            AllowBatching = true;
            DisableMessageDelay = false;
            RunAllCatalogScanDriversAsBatch = false;
            OnlyKeepLatestInAuxiliaryFileUpdater = true;
            RecordCertificateStatusUpdateTimes = true;
            MoveTempToHome = false;
            DisabledDrivers = new List<CatalogScanDriverType>();
            OldCatalogIndexScansToKeep = 49;
            OldWorkflowRunsToKeep = 49;
            WorkflowMaxAttempts = 5;

            AutoStartCatalogScanUpdate = false;
            AutoStartDownloadToCsv = false;
            AutoStartOwnersToCsv = false;
            AutoStartVerifiedPackagesToCsv = false;
            CatalogScanUpdateFrequency = TimeSpan.FromHours(6);
            DownloadToCsvFrequency = TimeSpan.FromHours(3);
            OwnersToCsvFrequency = TimeSpan.FromHours(3);
            VerifiedPackagesToCsvFrequency = TimeSpan.FromHours(3);
            WorkflowFrequency = TimeSpan.FromDays(1);
            KustoBlobIngestionTimeout = TimeSpan.FromHours(6);

            PackageContentFileExtensions = new List<string>();
            PackageContentMaxSizePerPackage = 1024 * 16;
            PackageContentMaxSizePerFile = 1024 * 16;

            WorkQueueName = "work";
            ExpandQueueName = "expand";
            CursorTableName = "cursors";
            CatalogIndexScanTableName = "catalogindexscans";
            CatalogPageScanTableName = "catalogpagescans";
            CatalogLeafScanTableName = "catalogleafscans";
            TaskStateTableName = "taskstate";
            CsvRecordTableName = "csvrecords";
            VersionSetAggregateTableName = "versionset";
            VersionSetContainerName = "versionset";
            KustoIngestionTableName = "kustoingestions";
            WorkflowRunTableName = "workflowruns";

            LatestPackageLeafTableName = "latestpackageleaves";
            PackageAssetContainerName = "packageassets";
            PackageAssemblyContainerName = "packageassemblies";
            PackageManifestContainerName = "packagemanifests";
            PackageReadmeContainerName = "packagereadmes";
            PackageSignatureContainerName = "packagesignatures";
            CatalogLeafItemContainerName = "catalogleafitems";
            PackageDownloadContainerName = "packagedownloads";
            PackageOwnerContainerName = "packageowners";
            VerifiedPackageContainerName = "verifiedpackages";
            PackageArchiveContainerName = "packagearchives";
            PackageArchiveEntryContainerName = "packagearchiveentries";
            SymbolPackageArchiveContainerName = "symbolpackagearchives";
            SymbolPackageArchiveEntryContainerName = "symbolpackagearchiveentries";
            PackageVersionTableName = "packageversions";
            PackageVersionContainerName = "packageversions";
            NuGetPackageExplorerContainerName = "nugetpackageexplorer";
            NuGetPackageExplorerFileContainerName = "nugetpackageexplorerfiles";
            PackageDeprecationContainerName = "packagedeprecations";
            PackageVulnerabilityContainerName = "packagevulnerabilities";
            PackageIconContainerName = "packageicons";
            PackageCompatibilityContainerName = "packagecompatibilities";
            PackageToCertificateTableName = "packagetocertificates";
            CertificateToPackageTableName = "certificatetopackages";
            PackageCertificateContainerName = "packagecertificates";
            CertificateContainerName = "certificates";
            PackageContentContainerName = "packagecontents";

            KustoConnectionString = null;
            KustoDatabaseName = null;
            KustoUseUserManagedIdentity = true;
            KustoApplyPartitioningPolicy = true;
            KustoTableNameFormat = "{0}";
            KustoTableFolder = string.Empty;
            KustoTableDocstringFormat = "See https://github.com/NuGet/Insights/blob/main/docs/tables/{0}.md";
            OldKustoIngestionsToKeep = 9;
            KustoIngestionMaxAttempts = 10;
            KustoValidationMaxAttempts = 3;

            EnableDiagnosticTracingToLogger = false;
        }

        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public int AppendResultStorageBucketCount { get; set; }
        public bool AllowBatching { get; set; }
        public bool DisableMessageDelay { get; set; }
        public bool RunAllCatalogScanDriversAsBatch { get; set; }
        public bool OnlyKeepLatestInAuxiliaryFileUpdater { get; set; }
        public bool RecordCertificateStatusUpdateTimes { get; set; }
        public bool MoveTempToHome { get; set; }
        public List<CatalogScanDriverType> DisabledDrivers { get; set; }
        public int OldCatalogIndexScansToKeep { get; set; }
        public int OldWorkflowRunsToKeep { get; set; }
        public int WorkflowMaxAttempts { get; set; }

        public bool AutoStartCatalogScanUpdate { get; set; }
        public bool AutoStartDownloadToCsv { get; set; }
        public bool AutoStartOwnersToCsv { get; set; }
        public bool AutoStartVerifiedPackagesToCsv { get; set; }
        public TimeSpan CatalogScanUpdateFrequency { get; set; }
        public TimeSpan DownloadToCsvFrequency { get; set; }
        public TimeSpan OwnersToCsvFrequency { get; set; }
        public TimeSpan VerifiedPackagesToCsvFrequency { get; set; }
        public TimeSpan WorkflowFrequency { get; set; }
        public TimeSpan KustoBlobIngestionTimeout { get; set; }

        /// <summary>
        /// The types of package content to index in <see cref="PackageContentToCsv.PackageContentToCsvDriver"/>. The
        /// order of this list is significant (entries are processed in the matching order). The values are treated as
        /// file path suffixes so be sure to include a dot (".") if you want file extension behavior.
        /// </summary>
        public List<string> PackageContentFileExtensions { get; set; }
        public int PackageContentMaxSizePerPackage { get; set; }
        public int PackageContentMaxSizePerFile { get; set; }

        public string WorkQueueName { get; set; }
        public string ExpandQueueName { get; set; }
        public string CursorTableName { get; set; }
        public string CatalogIndexScanTableName { get; set; }
        public string CatalogPageScanTableName { get; set; }
        public string CatalogLeafScanTableName { get; set; }
        public string TaskStateTableName { get; set; }
        public string CsvRecordTableName { get; set; }
        public string VersionSetAggregateTableName { get; set; }
        public string VersionSetContainerName { get; set; }
        public string KustoIngestionTableName { get; set; }
        public string WorkflowRunTableName { get; set; }

        public string LatestPackageLeafTableName { get; set; }
        public string PackageAssetContainerName { get; set; }
        public string PackageAssemblyContainerName { get; set; }
        public string PackageManifestContainerName { get; set; }
        public string PackageReadmeContainerName { get; set; }
        public string PackageSignatureContainerName { get; set; }
        public string CatalogLeafItemContainerName { get; set; }
        public string PackageDownloadContainerName { get; set; }
        public string PackageOwnerContainerName { get; set; }
        public string VerifiedPackageContainerName { get; set; }
        public string PackageArchiveContainerName { get; set; }
        public string PackageArchiveEntryContainerName { get; set; }
        public string SymbolPackageArchiveContainerName { get; set; }
        public string SymbolPackageArchiveEntryContainerName { get; set; }
        public string PackageVersionTableName { get; set; }
        public string PackageVersionContainerName { get; set; }
        public string NuGetPackageExplorerContainerName { get; set; }
        public string NuGetPackageExplorerFileContainerName { get; set; }
        public string PackageDeprecationContainerName { get; set; }
        public string PackageVulnerabilityContainerName { get; set; }
        public string PackageIconContainerName { get; set; }
        public string PackageCompatibilityContainerName { get; set; }
        public string PackageToCertificateTableName { get; set; }
        public string CertificateToPackageTableName { get; set; }
        public string PackageCertificateContainerName { get; set; }
        public string CertificateContainerName { get; set; }
        public string PackageContentContainerName { get; set; }

        public string KustoConnectionString { get; set; }
        public string KustoDatabaseName { get; set; }
        public bool KustoUseUserManagedIdentity { get; set; }

        /// <summary>
        /// A path to a certificate that will be loaded as a <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2"/>
        /// for Kusto AAD client app authentication.
        /// </summary>
        public string KustoClientCertificateContent { get; set; }

        public bool KustoApplyPartitioningPolicy { get; set; }
        public string KustoTableNameFormat { get; set; }
        public string KustoTableFolder { get; set; }
        public string KustoTableDocstringFormat { get; set; }
        public int OldKustoIngestionsToKeep { get; set; }
        public int KustoIngestionMaxAttempts { get; set; }
        public int KustoValidationMaxAttempts { get; set; }

        public bool EnableDiagnosticTracingToLogger { get; set; }
    }
}
