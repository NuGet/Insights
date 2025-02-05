// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using NuGet.Insights.Worker.ReferenceTracking;
using NuGet.Services.Validation;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    [CsvRecord]
    public partial record CertificateRecord : IAggregatedCsvRecord<CertificateRecord>, ICleanupOrphanCsvRecord
    {
        public CertificateRecord()
        {
        }

        public CertificateRecord(Guid scanId, DateTimeOffset scanTimestamp, CertificateInfo info)
        {
            ScanId = scanId;
            ScanTimestamp = scanTimestamp;
            ResultType = PackageCertificateResultType.Available;

            Fingerprint = info.Certificate.GetSHA256Base64UrlFingerprint();
            FingerprintSHA256Hex = info.Certificate.GetSHA256HexFingerprint();
            FingerprintSHA1Hex = info.Certificate.GetSHA1HexFingerprint();

            Subject = info.Certificate.GetSubjectXplat();
            Issuer = info.Certificate.GetIssuerXplat();
            NotBefore = info.Certificate.NotBefore.ToUniversalTime();
            NotAfter = info.Certificate.NotAfter.ToUniversalTime();
            SerialNumber = info.Certificate.SerialNumber;
            SignatureAlgorithmOid = info.Certificate.SignatureAlgorithm.Value;
            Version = info.Certificate.Version;
            Extensions = KustoDynamicSerializer.Serialize(info.Certificate.GetExtensions());
            PublicKeyOid = info.Certificate.PublicKey.Oid.Value;
            RawDataLength = info.Certificate.RawData.Length;
            RawData = info.Certificate.RawData.ToBase64();
            Policies = KustoDynamicSerializer.Serialize(info.Certificate.GetPolicies());

            if (info.Issuer is not null)
            {
                var root = info.Issuer;
                var chainLength = 2;
                while (root.Issuer is not null)
                {
                    root = root.Issuer;
                    chainLength++;
                }

                IssuerFingerprint = info.Issuer.Certificate.GetSHA256Base64UrlFingerprint();
                RootFingerprint = root.Certificate.GetSHA256Base64UrlFingerprint();
                ChainLength = chainLength;

                if (root.Certificate.Issuer != root.Certificate.Subject)
                {
                    throw new InvalidOperationException(
                        $"The root certifcate's ({RootFingerprint}) issuer should match the subject. " +
                        $"It was '{root.Certificate.Issuer}'.");
                }
            }

            if (info.Results.TryGetValue(EndCertificateUse.CodeSigning, out var codeSigning))
            {
                CodeSigningCommitTimestamp = info.Packages.Values.Max(x => x.CommitTimestamp);
                CodeSigningRevocationTime = codeSigning.RevocationTime;
                CodeSigningStatus = codeSigning.Status;
                CodeSigningStatusFlags = codeSigning.StatusFlags;
                CodeSigningStatusUpdateTime = codeSigning.StatusUpdateTime;
            }

            if (info.Results.TryGetValue(EndCertificateUse.Timestamping, out var timestamping))
            {
                TimestampingCommitTimestamp = info.Packages.Values.Max(x => x.CommitTimestamp);
                TimestampingRevocationTime = timestamping.RevocationTime;
                TimestampingStatus = timestamping.Status;
                TimestampingStatusFlags = timestamping.StatusFlags;
                TimestampingStatusUpdateTime = timestamping.StatusUpdateTime;
            }
        }

        [KustoIgnore]
        public Guid? ScanId { get; set; }

        [KustoIgnore]
        public DateTimeOffset? ScanTimestamp { get; set; }

        [Required]
        public PackageCertificateResultType ResultType { get; set; }

        /// <summary>
        /// SHA-256, base64 URL encoded fingerprint of the certificate.
        /// </summary>
        [BucketKey]
        [KustoPartitionKey]
        public string Fingerprint { get; set; }

        public string FingerprintSHA256Hex { get; set; }
        public string FingerprintSHA1Hex { get; set; }
        public string Subject { get; set; }
        public string Issuer { get; set; }

        [Required]
        public DateTimeOffset NotBefore { get; set; }

        [Required]
        public DateTimeOffset NotAfter { get; set; }

        public string SerialNumber { get; set; }
        public string SignatureAlgorithmOid { get; set; }

        [Required]
        public int Version { get; set; }

        [KustoType("dynamic")]
        public string Extensions { get; set; }

        public string PublicKeyOid { get; set; }

        [Required]
        public int RawDataLength { get; set; }

        public string RawData { get; set; }

        public string IssuerFingerprint { get; set; }
        public string RootFingerprint { get; set; }
        public int? ChainLength { get; set; }

        public DateTimeOffset? CodeSigningCommitTimestamp { get; set; }
        public EndCertificateStatus? CodeSigningStatus { get; set; }
        public X509ChainStatusFlags? CodeSigningStatusFlags { get; set; }
        public DateTimeOffset? CodeSigningStatusUpdateTime { get; set; }
        public DateTimeOffset? CodeSigningRevocationTime { get; set; }

        public DateTimeOffset? TimestampingCommitTimestamp { get; set; }
        public EndCertificateStatus? TimestampingStatus { get; set; }
        public X509ChainStatusFlags? TimestampingStatusFlags { get; set; }
        public DateTimeOffset? TimestampingStatusUpdateTime { get; set; }
        public DateTimeOffset? TimestampingRevocationTime { get; set; }

        [KustoType("dynamic")]
        public string Policies { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.c";
        public static string CleanupOrphanRecordsMessageSchemaName => "co.c";
        public static IEqualityComparer<CertificateRecord> KeyComparer { get; } = CertificateRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(Fingerprint)];

        public static List<CertificateRecord> Prune(List<CertificateRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
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
                        GuardChange(x.FingerprintSHA256Hex, x.FingerprintSHA256Hex, aggregate.FingerprintSHA256Hex, nameof(FingerprintSHA256Hex));
                        GuardChange(x.FingerprintSHA256Hex, x.FingerprintSHA1Hex, aggregate.FingerprintSHA1Hex, nameof(FingerprintSHA1Hex));
                        GuardChange(x.FingerprintSHA256Hex, x.Subject, aggregate.Subject, nameof(Subject));
                        GuardChange(x.FingerprintSHA256Hex, x.Issuer, aggregate.Issuer, nameof(Issuer));
                        GuardChange(x.FingerprintSHA256Hex, x.NotBefore, aggregate.NotBefore, nameof(NotBefore));
                        GuardChange(x.FingerprintSHA256Hex, x.NotAfter, aggregate.NotAfter, nameof(NotAfter));
                        GuardChange(x.FingerprintSHA256Hex, x.SerialNumber, aggregate.SerialNumber, nameof(SerialNumber));
                        GuardChange(x.FingerprintSHA256Hex, x.SignatureAlgorithmOid, aggregate.SignatureAlgorithmOid, nameof(SignatureAlgorithmOid));
                        GuardChange(x.FingerprintSHA256Hex, x.Version, aggregate.Version, nameof(Version));
                        GuardChange(x.FingerprintSHA256Hex, x.Extensions, aggregate.Extensions, nameof(Extensions));
                        GuardChange(x.FingerprintSHA256Hex, x.PublicKeyOid, aggregate.PublicKeyOid, nameof(PublicKeyOid));
                        GuardChange(x.FingerprintSHA256Hex, x.RawDataLength, aggregate.RawDataLength, nameof(RawDataLength));
                        GuardChange(x.FingerprintSHA256Hex, x.RawData, aggregate.RawData, nameof(RawData));

                        // These properties can change. Most of the time they're the same but chains are not guaranteed to be unique.
                        GuardChange(x.FingerprintSHA256Hex, x.IssuerFingerprint, aggregate.IssuerFingerprint, nameof(IssuerFingerprint), logger);
                        GuardChange(x.FingerprintSHA256Hex, x.RootFingerprint, aggregate.RootFingerprint, nameof(RootFingerprint), logger);
                        GuardChange(x.FingerprintSHA256Hex, x.ChainLength, aggregate.ChainLength, nameof(ChainLength), logger);
                    }

                    return aggregate;
                })
                .Order()
                .ToList();

            foreach (var record in pruned)
            {
                if (!options.Value.RecordCertificateStatus)
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

        private static void GuardChange(string fingerprint, string a, string b, string propertyName, ILogger logger = null)
        {
            // Coalesce to empty string since the CSV reader can't differentiate an empty string and a null string.
            GuardChange<string>(fingerprint, a ?? string.Empty, b ?? string.Empty, propertyName, logger);
        }

        private static void GuardChange<T>(string fingerprint, T a, T b, string propertyName, ILogger logger = null)
        {
            if (!Equals(a, b))
            {
                if (logger is not null)
                {
                    logger.LogWarning(
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

        public int CompareTo(CertificateRecord other)
        {
            return string.CompareOrdinal(Fingerprint, other.Fingerprint);
        }

        public class CertificateRecordKeyComparer : IEqualityComparer<CertificateRecord>
        {
            public static CertificateRecordKeyComparer Instance { get; } = new();

            public bool Equals(CertificateRecord x, CertificateRecord y)
            {
                if ((x is null && y is null)
                    || ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Fingerprint == y.Fingerprint;
            }

            public int GetHashCode([DisallowNull] CertificateRecord obj)
            {
                return obj.Fingerprint.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}
