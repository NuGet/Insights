// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Validation;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public partial record CertificateRecord : ICsvRecord
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

        public PackageCertificateResultType ResultType { get; set; }

        /// <summary>
        /// SHA-256, base64 URL encoded fingerprint of the certificate.
        /// </summary>
        [KustoPartitionKey]
        public string Fingerprint { get; set; }

        public string FingerprintSHA256Hex { get; set; }
        public string FingerprintSHA1Hex { get; set; }
        public string Subject { get; set; }
        public string Issuer { get; set; }
        public DateTimeOffset NotBefore { get; set; }
        public DateTimeOffset NotAfter { get; set; }
        public string SerialNumber { get; set; }
        public string SignatureAlgorithmOid { get; set; }
        public int Version { get; set; }
        [KustoType("dynamic")]
        public string Extensions { get; set; }
        public string PublicKeyOid { get; set; }
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
    }
}
