// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using Validation.PackageSigning.ValidateCertificate;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class CertificateDataBuilder
    {
        private readonly ICertificateVerifier _verifier;
        private readonly ILogger _logger;

        public CertificateDataBuilder(ICertificateVerifier verifier, ILogger logger)
        {
            _verifier = verifier;
            _logger = logger;
            ScanId = Guid.NewGuid();
            ScanTimestamp = DateTimeOffset.UtcNow;
        }

        public Guid ScanId { get; }
        public DateTimeOffset ScanTimestamp { get; }

        public Dictionary<PackageIdentity, Dictionary<string, CertificateRelationshipTypes>> Relationships { get; } = new();

        /// <summary>
        /// A mapping from URL-safe base64 encoded SHA-256 fingerprint to certificate info for all certificates found in this batch of
        /// packages (catalog leaf scans). The fingerprint is produced using <see cref="Worker.X509Certificate2Extensions.GetSHA256Base64UrlFingerprint(X509Certificate2)"/>.
        /// </summary>
        public Dictionary<string, CertificateInfo> FingerprintToInfo { get; } = new();

        public void AddPackage(
            PackageIdentity package,
            ICatalogLeafItem leafItem,
            PrimarySignature signature)
        {
            InitializePackage(package);

            AddSignature(package, leafItem, signature, signature.SignedCms.Certificates);

            if (signature.Type == SignatureType.Author)
            {
                var repositorySignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);
                AddSignature(package, leafItem, repositorySignature, signature.SignedCms.Certificates);
            }
        }

        public void DeletePackage(PackageIdentity package)
        {
            InitializePackage(package);
        }

        public void ClearPackages()
        {
            Relationships.Clear();
        }

        private void InitializePackage(PackageIdentity package)
        {
            Relationships.Add(package, new());
        }

        private void AddSignature(
            PackageIdentity package,
            ICatalogLeafItem leafItem,
            Signature signature,
            X509Certificate2Collection extraCertificates)
        {
            AddSignerInfo(
                package,
                leafItem,
                signature.SignerInfo,
                EndCertificateUse.CodeSigning,
                endCertificateRelationship: signature.Type switch
                {
                    SignatureType.Author => CertificateRelationshipTypes.IsAuthorCodeSignedBy,
                    SignatureType.Repository => CertificateRelationshipTypes.IsRepositoryCodeSignedBy,
                    _ => throw new NotImplementedException(),
                },
                extraCertificatesRelationship: CertificateRelationshipTypes.PrimarySignedCmsContains,
                extraCertificates);

            Timestamp timestamp;
            try
            {
                timestamp = signature.Timestamps.Single();
                AddSignerInfo(
                    package,
                    leafItem,
                    timestamp.SignerInfo,
                    EndCertificateUse.Timestamping,
                    endCertificateRelationship: signature.Type switch
                    {
                        SignatureType.Author => CertificateRelationshipTypes.IsAuthorTimestampedBy,
                        SignatureType.Repository => CertificateRelationshipTypes.IsRepositoryTimestampedBy,
                        _ => throw new NotImplementedException(),
                    },
                    extraCertificatesRelationship: signature.Type switch
                    {
                        SignatureType.Author => CertificateRelationshipTypes.AuthorTimestampSignedCmsContains,
                        SignatureType.Repository => CertificateRelationshipTypes.RepositoryTimestampSignedCmsContains,
                        _ => throw new NotImplementedException(),
                    },
                    timestamp.SignedCms.Certificates);
            }
            catch (CryptographicException ex) when (ex.IsInvalidDataException())
            {
                // Ignore this error since this is captured by the PackageSignatureToCsv driver.
                _logger.LogWarning(ex, "The signature in package {Id} {Version} has invalid timestamp ASN.1.", package.Id, package.Version);
            }
        }

        private void AddSignerInfo(
            PackageIdentity package,
            ICatalogLeafItem leafItem,
            SignerInfo signerInfo,
            EndCertificateUse endCertificateUse,
            CertificateRelationshipTypes endCertificateRelationship,
            CertificateRelationshipTypes extraCertificatesRelationship,
            X509Certificate2Collection extraCertificates)
        {
            var endInfo = AddCertificate(package, leafItem, signerInfo.Certificate, extraCertificates);
            Assert(package, endInfo.Fingerprint, endCertificateRelationship);

            // Track the uses for this certificate, so we can run trust verification. Don't replace existing
            // results so that we can reuse verification outcome.
            endInfo.Results.TryAdd(endCertificateUse, null);

            foreach (var extraCertificate in extraCertificates)
            {
                var extraInfo = AddCertificate(package, leafItem, extraCertificate, extraCertificates);
                Assert(package, extraInfo.Fingerprint, extraCertificatesRelationship);
            }
        }

        public CertificateInfo AddCertificate(
            PackageIdentity package,
            ICatalogLeafItem leafItem,
            X509Certificate2 certificate,
            X509Certificate2Collection extraCertificates)
        {
            var fingerprint = certificate.GetSHA256Base64UrlFingerprint();
            if (!FingerprintToInfo.TryGetValue(fingerprint, out var info))
            {
                info = new CertificateInfo(fingerprint, certificate, extraCertificates);
                FingerprintToInfo.Add(fingerprint, info);
            }

            info.Packages[package] = leafItem;

            return info;
        }

        public void VerifyCertificatesAndConnect()
        {
            foreach ((var fingerprint, var info) in FingerprintToInfo.ToList())
            {
                // Run certificate verifications that have not been done yet.
                foreach (var use in info.Results.Where(x => x.Value == null).Select(x => x.Key).ToList())
                {
                    info.Results[use] = use switch
                    {
                        EndCertificateUse.CodeSigning => _verifier.VerifyCodeSigningCertificate(info.Certificate, info.Extra.ToList(), GetChainInfo),
                        EndCertificateUse.Timestamping => _verifier.VerifyTimestampingCertificate(info.Certificate, info.Extra.ToList(), GetChainInfo),
                        _ => throw new NotImplementedException(),
                    };
                }
            }

            ConnectChainRelationships();
        }

        private static ChainInfo GetChainInfo(X509Chain chain)
        {
            return new ChainInfo(chain
                .ChainElements
                .Select(x => new X509Certificate2(x.Certificate))
                .Select(x => (Fingerprint: x.GetSHA256Base64UrlFingerprint(), Certificate: x))
                .ToList());
        }

        private void ConnectChainRelationships()
        {
            foreach ((var fingerprint, var info) in FingerprintToInfo.ToList())
            {
                // Add chain relationships to all packages that use this certificate as an end certificate.
                foreach ((var use, var result) in info.Results)
                {
                    foreach ((var package, var leafItem) in info.Packages)
                    {
                        if (!Relationships.TryGetValue(package, out var fingerprints)
                            || !fingerprints.TryGetValue(fingerprint, out var relationships))
                        {
                            continue;
                        }

                        var chainRelationship = CertificateRelationshipTypes.None;

                        switch (use)
                        {
                            case EndCertificateUse.CodeSigning:
                                if (relationships.HasFlag(CertificateRelationshipTypes.IsAuthorCodeSignedBy))
                                {
                                    chainRelationship |= CertificateRelationshipTypes.AuthorCodeSigningChainContains;
                                }

                                if (relationships.HasFlag(CertificateRelationshipTypes.IsRepositoryCodeSignedBy))
                                {
                                    chainRelationship |= CertificateRelationshipTypes.RepositoryCodeSigningChainContains;
                                }

                                break;
                            case EndCertificateUse.Timestamping:
                                if (relationships.HasFlag(CertificateRelationshipTypes.IsAuthorTimestampedBy))
                                {
                                    chainRelationship |= CertificateRelationshipTypes.AuthorTimestampingChainContains;
                                }

                                if (relationships.HasFlag(CertificateRelationshipTypes.IsRepositoryTimestampedBy))
                                {
                                    chainRelationship |= CertificateRelationshipTypes.RepositoryTimestampingChainContains;
                                }

                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        for (var i = result.ChainInfo.Certificates.Count - 1; i >= 0; i--)
                        {
                            (var chainFingerprint, var chainCertificate) = result.ChainInfo.Certificates[i];
                            var chainCertificateInfo = AddCertificate(package, leafItem, chainCertificate, info.Extra);
                            Assert(package, chainFingerprint, chainRelationship);

                            // Associate certificates with their issuer
                            if (i < result.ChainInfo.Certificates.Count - 1)
                            {
                                var issuerInfo = FingerprintToInfo[result.ChainInfo.Certificates[i + 1].Fingerprint];
                                if (issuerInfo.Certificate.Subject != chainCertificate.Issuer)
                                {
                                    throw new InvalidOperationException(
                                        $"For certificate '{chainFingerprint}', the issuer name does not match the next certificate's subject name. " +
                                        $"Current issuer: '{chainCertificate.Issuer}', " +
                                        $"Next subject: '{issuerInfo.Certificate.Subject}'.");
                                }

                                if (chainCertificateInfo.Issuer is not null
                                    && chainCertificateInfo.Issuer.Fingerprint != issuerInfo.Fingerprint)
                                {
                                    _logger.LogWarning(
                                        "A different issuer was found for certificate {FingerprintSHA256}. It changed from {Before} to {After}.",
                                        chainCertificate.GetSHA256HexFingerprint(),
                                        chainCertificateInfo.Issuer.Certificate.GetSHA256HexFingerprint(),
                                        issuerInfo.Certificate.GetSHA256HexFingerprint());
                                }

                                chainCertificateInfo.Issuer = issuerInfo;
                            }
                        }
                    }
                }
            }
        }

        private void Assert(PackageIdentity package, string fingerprint, CertificateRelationshipTypes type)
        {
            var relationships = Relationships[package];

            if (!relationships.TryGetValue(fingerprint, out var types))
            {
                types = CertificateRelationshipTypes.None;
            }

            relationships[fingerprint] = types | type;
        }
    }
}
