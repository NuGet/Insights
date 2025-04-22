// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using NuGet.Insights.ReferenceTracking;
using Validation.PackageSigning.ValidateCertificate;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    /// <summary>
    /// This driver discovers and records the following fundamental package to certificate relationships:
    /// 
    ///   - "package PRIMARY-SIGNED-CMS-CONTAINS certificate"
    ///     The certificate is in the primary signature's signed CMS. This typically has the certificates of both the
    ///     repository and author code signing certificate chains. Most often this is 3 for the repository signature
    ///     and 3 more (6 total) if there is an author signature.
    ///
    ///   - "package AUTHOR-TIMESTAMP-SIGNED-CMS-CONTAINS certificate"
    ///     The certificate is in the author timestamp's signed CMS. Most often this is 3 certificates.
    ///
    ///   - "package REPOSITORY-TIMESTAMP-SIGNED-CMS-CONTAINS certificate"
    ///     The certificate is in the repository timestamp's signed CMS. Most often this is 3 certificates.
    ///     
    ///   - "package IS-AUTHOR-CODE-SIGNED-BY certificate"
    ///     The certificate is the end certificate for the author code signature.
    ///     
    ///   - "package IS-REPOSITORY-CODE-SIGNED-BY certificate"
    ///     The certificate is the end certificate for the repository code signature.
    ///     
    ///   - "package IS-AUTHOR-TIMESTAMPED-BY certificate"
    ///     The certificate is the end certificate for the author timestamp.
    ///     
    ///   - "package IS-REPOSITORY-TIMESTAMPED-BY certificate"
    ///     The certificate is the end certificate for the repository timestamp.
    /// 
    /// This driver also discovers the following certificate to certificate relationship:
    ///
    ///   - "certificate IS-ISSUED-BY certificate"
    ///
    /// For easy of querying and denormalization purposes, this driver implies the following additional package to
    /// certificate relationships from the aforementioned relationships.
    ///
    ///   - "package AUTHOR-CODE-SIGNING-CHAIN-CONTAINS certificate"
    ///   - "package AUTHOR-TIMESTAMPING-CHAIN-CONTAINS certificate"
    ///   - "package REPOSITORY-CODE-SIGNING-CHAIN-CONTAINS certificate"
    ///   - "package REPOSITORY-TIMESTAMPING-CHAIN-CONTAINS certificate"
    ///
    /// Finally, this driver extracts interesting metadata about individual certificates.
    /// </summary>
    public class PackageCertificateToCsvDriver :
        ICatalogLeafToCsvBatchDriver<PackageCertificateRecord, CertificateRecord>,
        ICsvResultStorage<PackageCertificateRecord>,
        ICsvResultStorage<CertificateRecord>
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly ReferenceTracker _referenceTracker;
        private readonly ICertificateVerifier _verifier;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<PackageCertificateToCsvDriver> _logger;

        public PackageCertificateToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            ReferenceTracker referenceTracker,
            ICertificateVerifier verifier,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<PackageCertificateToCsvDriver> logger)
        {
            _initializationState = ContainerInitializationState.New(InitializeInternalAsync, DestroyInternalAsync);
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _referenceTracker = referenceTracker;
            _verifier = verifier;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        public async Task DestroyAsync()
        {
            await _initializationState.DestroyAsync();
        }

        private async Task InitializeInternalAsync()
        {
            await Task.WhenAll(
                _packageFileService.InitializeAsync(),
                _referenceTracker.InitializeAsync(
                    _options.Value.PackageToCertificateTableName,
                    _options.Value.CertificateToPackageTableName));
        }

        private async Task DestroyInternalAsync()
        {
            await _referenceTracker.DestroyAsync(
                _options.Value.PackageToCertificateTableName,
                _options.Value.CertificateToPackageTableName);
        }

        public bool SingleMessagePerId => false;

        string ICsvResultStorage<PackageCertificateRecord>.ResultContainerName => _options.Value.PackageCertificateContainerName;
        string ICsvResultStorage<CertificateRecord>.ResultContainerName => _options.Value.CertificateContainerName;

        public async Task<BatchMessageProcessorResult<CsvRecordSets<PackageCertificateRecord, CertificateRecord>, CatalogLeafScan>>
            ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var failed = new List<CatalogLeafScan>();
            var builder = new CertificateDataBuilder(_verifier, _logger);
            var packageCertificates = new List<PackageCertificateRecord>();

            foreach (var group in leafScans.GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var packageId = group.Key.ToLowerInvariant();
                var leafItems = group.Cast<CatalogLeafScan>().ToList();
                try
                {
                    packageCertificates.AddRange(await ProcessPackageIdAsync(builder, packageId, leafItems));
                }
                catch (Exception ex) when (leafScans.Count != 1)
                {
                    _logger.LogError(ex, "Loading package certificate info failed for {Id} with {Count} versions.", group.Key, leafItems.Count);
                    failed.AddRange(group);
                }
            }

            var certificates = GetCertificateRecords(builder);

            return new BatchMessageProcessorResult<CsvRecordSets<PackageCertificateRecord, CertificateRecord>, CatalogLeafScan>(
                new CsvRecordSets<PackageCertificateRecord, CertificateRecord>(
                    packageCertificates,
                    certificates),
                failed);
        }

        private async Task<List<PackageCertificateRecord>> ProcessPackageIdAsync(
            CertificateDataBuilder builder,
            string packageId,
            IReadOnlyList<CatalogLeafScan> leafItems)
        {
            // Clear package state from a previous iteration, but leave certificate validation results.
            builder.ClearPackages();

            await PopulateCertificatesAsync(builder, packageId, leafItems);

            await WriteReferencesAsync(builder, packageId);

            return await GetPackageCertificateRecordsAsync(builder, packageId, leafItems);
        }

        private async Task PopulateCertificatesAsync(
            CertificateDataBuilder builder,
            string packageId,
            IReadOnlyList<CatalogLeafScan> leafItems)
        {
            foreach (var item in leafItems)
            {
                var packageIdentity = GetIdentity(packageId, item);
                if (item.LeafType == CatalogLeafType.PackageDelete)
                {
                    builder.DeletePackage(packageIdentity);
                }
                else
                {
                    var signature = await _packageFileService.GetPrimarySignatureAsync(item.ToPackageIdentityCommit());
                    if (signature == null)
                    {
                        builder.DeletePackage(packageIdentity);
                    }
                    else
                    {
                        builder.AddPackage(packageIdentity, item, signature);
                    }
                }
            }

            builder.VerifyCertificatesAndConnect();
        }

        private async Task WriteReferencesAsync(
            CertificateDataBuilder builder,
            string packageId)
        {
            if (packageId.Contains(ReferenceTracker.Separator, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    $"Skipping writing references package ID '{{Id}}' because it contains a " +
                    $"'{ReferenceTracker.Separator}'. This is not valid for package IDs.",
                    packageId);
                return;
            }

            var versionToReferences = new Dictionary<string, IReadOnlySet<SubjectEdge>>();
            foreach ((var package, var relationships) in builder.Relationships)
            {
                if (!relationships.Any())
                {
                    versionToReferences.Add(package.Version, ReferenceTracker.EmptySet);
                    continue;
                }

                var references = new HashSet<SubjectEdge>();

                foreach ((var fingerprint, var types) in relationships)
                {
                    references.Add(new SubjectEdge(
                        fingerprint,
                        string.Empty,
                        MessagePackSerializer.Serialize(types, NuGetInsightsMessagePack.Options)));
                }

                versionToReferences.Add(package.Version, references);
            }

            await _referenceTracker.SetReferencesAsync(
                _options.Value.PackageToCertificateTableName,
                _options.Value.CertificateToPackageTableName,
                ReferenceTypes.Package,
                ReferenceTypes.Certificate,
                packageId,
                versionToReferences);
        }

        private async Task<List<PackageCertificateRecord>> GetPackageCertificateRecordsAsync(
            CertificateDataBuilder builder,
            string packageId,
            IReadOnlyList<ICatalogLeafItem> leafItems)
        {
            var records = new List<PackageCertificateRecord>();

            foreach (var item in leafItems)
            {
                var packageIdentity = GetIdentity(packageId, item);

                if (item.LeafType == CatalogLeafType.PackageDelete)
                {
                    var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.LeafType, item.Url);
                    records.Add(new PackageCertificateRecord(builder.ScanId, builder.ScanTimestamp, leaf));
                }
                else
                {
                    var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.LeafType, item.Url);
                    records.AddRange(builder
                        .Relationships[packageIdentity]
                        .Select(pair => new PackageCertificateRecord(
                            builder.ScanId,
                            builder.ScanTimestamp,
                            leaf)
                        {
                            Fingerprint = pair.Key,
                            RelationshipTypes = pair.Value,
                        }));
                }
            }

            return records;
        }

        private List<CertificateRecord> GetCertificateRecords(
            CertificateDataBuilder builder)
        {
            return builder
                .FingerprintToInfo
                .Values
                .Select(x => new CertificateRecord(builder.ScanId, builder.ScanTimestamp, x))
                .ToList();
        }

        private static PackageIdentity GetIdentity(string packageId, ICatalogLeafItem item)
        {
            return new PackageIdentity(
                packageId,
                item.ParsePackageVersion().ToNormalizedString().ToLowerInvariant());
        }
    }
}
