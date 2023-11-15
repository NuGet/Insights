// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _referenceTracker = referenceTracker;
            _verifier = verifier;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
            await _referenceTracker.InitializeAsync(
                _options.Value.PackageToCertificateTableName,
                _options.Value.CertificateToPackageTableName);
        }

        public async Task DestroyAsync()
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
            var packageCertificates = new List<CsvRecordSet<PackageCertificateRecord>>();

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

        private async Task<List<CsvRecordSet<PackageCertificateRecord>>> ProcessPackageIdAsync(
            CertificateDataBuilder builder,
            string packageId,
            IReadOnlyList<ICatalogLeafItem> leafItems)
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
            IReadOnlyList<ICatalogLeafItem> leafItems)
        {
            foreach (var item in leafItems)
            {
                var packageIdentity = GetIdentity(packageId, item);
                if (item.Type == CatalogLeafType.PackageDelete)
                {
                    builder.DeletePackage(packageIdentity);
                }
                else
                {
                    var signature = await _packageFileService.GetPrimarySignatureAsync(item);
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

        private async Task<List<CsvRecordSet<PackageCertificateRecord>>> GetPackageCertificateRecordsAsync(
            CertificateDataBuilder builder,
            string packageId,
            IReadOnlyList<ICatalogLeafItem> leafItems)
        {
            var csvSets = new List<CsvRecordSet<PackageCertificateRecord>>();

            foreach (var item in leafItems)
            {
                var packageIdentity = GetIdentity(packageId, item);
                var bucketKey = PackageRecord.GetBucketKey(item);

                if (item.Type == CatalogLeafType.PackageDelete)
                {
                    var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                    csvSets.Add(new CsvRecordSet<PackageCertificateRecord>(
                        bucketKey,
                        new List<PackageCertificateRecord>
                        {
                            new PackageCertificateRecord(builder.ScanId, builder.ScanTimestamp, leaf),
                        }));
                }
                else
                {
                    var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                    csvSets.Add(new CsvRecordSet<PackageCertificateRecord>(
                        bucketKey,
                        builder
                            .Relationships[packageIdentity]
                            .Select(pair => new PackageCertificateRecord(
                                builder.ScanId,
                                builder.ScanTimestamp,
                                leaf)
                            {
                                Fingerprint = pair.Key,
                                RelationshipTypes = pair.Value,
                            })
                            .ToList()));
                }
            }

            return csvSets;
        }

        private List<CsvRecordSet<CertificateRecord>> GetCertificateRecords(
            CertificateDataBuilder builder)
        {
            return builder
                .FingerprintToInfo
                .Values
                .Select(x => new CertificateRecord(builder.ScanId, builder.ScanTimestamp, x))
                .Select(x => new CsvRecordSet<CertificateRecord>(x.Fingerprint, new[] { x }))
                .ToList();
        }

        private static PackageIdentity GetIdentity(string packageId, ICatalogLeafItem item)
        {
            return new PackageIdentity(
                packageId,
                item.ParsePackageVersion().ToNormalizedString().ToLowerInvariant());
        }

        List<PackageCertificateRecord> ICsvResultStorage<PackageCertificateRecord>.Prune(List<PackageCertificateRecord> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        private void GuardChange(string fingerprint, string a, string b, string propertyName, bool log)
        {
            // Coalesce to empty string since the CSV reader can't differentiate an empty string and a null string.
            GuardChange<string>(fingerprint, a ?? string.Empty, b ?? string.Empty, propertyName, log);
        }

        private void GuardChange<T>(string fingerprint, T a, T b, string propertyName, bool log)
        {
            if (!Equals(a, b))
            {
                if (log)
                {
                    _logger.LogWarning(
                        "The {PropertyName} property on the certificate record {FingerprintSHA256} changed from {Before} to {After}.",
                        propertyName,
                        fingerprint,
                        a,
                        b);
                }
                else
                {
                    throw new InvalidOperationException($"The {propertyName} property on the certificate record {fingerprint} changed from {a} to {b}.");
                }
            }
        }

        List<CertificateRecord> ICsvResultStorage<CertificateRecord>.Prune(List<CertificateRecord> records, bool isFinalPrune)
        {
            var pruned = records
                .GroupBy(x => x.Fingerprint) // Group by SHA-256 fingerprint
                .Where(g => !isFinalPrune || g.All(x => x.ResultType != PackageCertificateResultType.Deleted))
                .Select(g =>
                {
                    // Prefer the most recent results (scan timestamp).
                    var items = g.OrderByDescending(x => x.ScanTimestamp ?? DateTimeOffset.MinValue).ToList();

                    var aggregate = items.First();
                    foreach (var x in items.Skip(1))
                    {
                        // Take the newest code signing and timestamping results
                        if (x.CodeSigningCommitTimestamp.GetValueOrDefault() > aggregate.CodeSigningCommitTimestamp.GetValueOrDefault())
                        {
                            aggregate.CodeSigningCommitTimestamp = x.CodeSigningCommitTimestamp;
                            aggregate.CodeSigningRevocationTime = x.CodeSigningRevocationTime;
                            aggregate.CodeSigningStatus = x.CodeSigningStatus;
                            aggregate.CodeSigningStatusFlags = x.CodeSigningStatusFlags;
                            aggregate.CodeSigningStatusUpdateTime = x.CodeSigningStatusUpdateTime;
                        }

                        if (x.TimestampingCommitTimestamp.GetValueOrDefault() > aggregate.TimestampingCommitTimestamp.GetValueOrDefault())
                        {
                            aggregate.TimestampingCommitTimestamp = x.TimestampingCommitTimestamp;
                            aggregate.TimestampingRevocationTime = x.TimestampingRevocationTime;
                            aggregate.TimestampingStatus = x.TimestampingStatus;
                            aggregate.TimestampingStatusFlags = x.TimestampingStatusFlags;
                            aggregate.TimestampingStatusUpdateTime = x.TimestampingStatusUpdateTime;
                        }

                        // These properties are immutable. They exist in the certificate metadata.
                        GuardChange(x.FingerprintSHA256Hex, x.FingerprintSHA256Hex, aggregate.FingerprintSHA256Hex, nameof(CertificateRecord.FingerprintSHA256Hex), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.FingerprintSHA1Hex, aggregate.FingerprintSHA1Hex, nameof(CertificateRecord.FingerprintSHA1Hex), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.Subject, aggregate.Subject, nameof(CertificateRecord.Subject), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.Issuer, aggregate.Issuer, nameof(CertificateRecord.Issuer), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.NotBefore, aggregate.NotBefore, nameof(CertificateRecord.NotBefore), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.NotAfter, aggregate.NotAfter, nameof(CertificateRecord.NotAfter), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.SerialNumber, aggregate.SerialNumber, nameof(CertificateRecord.SerialNumber), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.SignatureAlgorithmOid, aggregate.SignatureAlgorithmOid, nameof(CertificateRecord.SignatureAlgorithmOid), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.Version, aggregate.Version, nameof(CertificateRecord.Version), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.Extensions, aggregate.Extensions, nameof(CertificateRecord.Extensions), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.PublicKeyOid, aggregate.PublicKeyOid, nameof(CertificateRecord.PublicKeyOid), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.RawDataLength, aggregate.RawDataLength, nameof(CertificateRecord.RawDataLength), log: false);
                        GuardChange(x.FingerprintSHA256Hex, x.RawData, aggregate.RawData, nameof(CertificateRecord.RawData), log: false);

                        // These properties can change. Most of the time they're the same but chains are not guaranteed to be unique.
                        GuardChange(x.FingerprintSHA256Hex, x.IssuerFingerprint, aggregate.IssuerFingerprint, nameof(CertificateRecord.IssuerFingerprint), log: true);
                        GuardChange(x.FingerprintSHA256Hex, x.RootFingerprint, aggregate.RootFingerprint, nameof(CertificateRecord.RootFingerprint), log: true);
                        GuardChange(x.FingerprintSHA256Hex, x.ChainLength, aggregate.ChainLength, nameof(CertificateRecord.ChainLength), log: true);
                    }

                    return aggregate;
                })
                .OrderBy(x => x.Fingerprint, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var record in pruned)
            {
                if (!_options.Value.RecordCertificateStatus)
                {
                    record.CodeSigningStatus = null;
                    record.CodeSigningStatusFlags = null;
                    record.CodeSigningStatusUpdateTime = null;
                    record.TimestampingStatus = null;
                    record.TimestampingStatusFlags = null;
                    record.TimestampingStatusUpdateTime = null;
                }

                record.ScanId = null;
                record.ScanTimestamp = null;
            }

            return pruned;
        }
    }
}
