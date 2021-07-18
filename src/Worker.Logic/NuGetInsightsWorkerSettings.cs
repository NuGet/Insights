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
            RunAllCatalogScanDriversAsBatch = false;
            OnlyKeepLatestInAuxiliaryFileUpdater = true;
            MoveTempToHome = false;
            DisabledDrivers = new List<CatalogScanDriverType>();
            OldCatalogIndexScansToKeep = 9;
            OldWorkflowRunsToKeep = 9;

            AutoStartCatalogScanUpdate = false;
            AutoStartDownloadToCsv = false;
            AutoStartOwnersToCsv = false;
            CatalogScanUpdateFrequency = TimeSpan.FromHours(6);
            DownloadToCsvFrequency = TimeSpan.FromHours(3);
            OwnersToCsvFrequency = TimeSpan.FromHours(3);

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
            PackageSignatureContainerName = "packagesignatures";
            CatalogLeafItemContainerName = "catalogleafitems";
            PackageDownloadContainerName = "packagedownloads";
            PackageOwnerContainerName = "packageowners";
            PackageArchiveContainerName = "packagearchives";
            PackageArchiveEntryContainerName = "packagearchiveentries";
            PackageVersionTableName = "packageversions";
            PackageVersionContainerName = "packageversions";
            NuGetPackageExplorerContainerName = "nugetpackageexplorer";
            NuGetPackageExplorerFileContainerName = "nugetpackageexplorerfiles";
            PackageDeprecationContainerName = "packagedeprecations";
            PackageVulnerabilityContainerName = "packagevulnerabilities";
            PackageCompatibilityContainerName = "packagecompatibility";

            KustoConnectionString = null;
            KustoDatabaseName = null;
            KustoUseUserManagedIdentity = true;
            KustoApplyPartitioningPolicy = true;
            KustoTableNameFormat = "{0}";
            OldKustoIngestionsToKeep = 9;
        }

        public bool UseBulkEnqueueStrategy { get; set; }
        public int BulkEnqueueThreshold { get; set; }
        public int EnqueueWorkers { get; set; }
        public int AppendResultStorageBucketCount { get; set; }
        public bool AllowBatching { get; set; }
        public bool RunAllCatalogScanDriversAsBatch { get; set; }
        public bool OnlyKeepLatestInAuxiliaryFileUpdater { get; set; }
        public bool MoveTempToHome { get; set; }
        public List<CatalogScanDriverType> DisabledDrivers { get; set; }
        public int OldCatalogIndexScansToKeep { get; set; }
        public int OldWorkflowRunsToKeep { get; set; }

        public bool AutoStartCatalogScanUpdate { get; set; }
        public bool AutoStartDownloadToCsv { get; set; }
        public bool AutoStartOwnersToCsv { get; set; }
        public TimeSpan CatalogScanUpdateFrequency { get; set; }
        public TimeSpan DownloadToCsvFrequency { get; set; }
        public TimeSpan OwnersToCsvFrequency { get; set; }

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
        public string PackageSignatureContainerName { get; set; }
        public string CatalogLeafItemContainerName { get; set; }
        public string PackageDownloadContainerName { get; set; }
        public string PackageOwnerContainerName { get; set; }
        public string PackageArchiveContainerName { get; set; }
        public string PackageArchiveEntryContainerName { get; set; }
        public string PackageVersionTableName { get; set; }
        public string PackageVersionContainerName { get; set; }
        public string NuGetPackageExplorerContainerName { get; set; }
        public string NuGetPackageExplorerFileContainerName { get; set; }
        public string PackageDeprecationContainerName { get; set; }
        public string PackageVulnerabilityContainerName { get; set; }
        public string PackageCompatibilityContainerName { get; set; }

        public string KustoConnectionString { get; set; }
        public string KustoDatabaseName { get; set; }
        public bool KustoUseUserManagedIdentity { get; set; }
        public bool KustoApplyPartitioningPolicy { get; set; }
        public string KustoTableNameFormat { get; set; }
        public int OldKustoIngestionsToKeep { get; set; }
    }
}
