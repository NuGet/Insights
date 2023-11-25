// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Validation;
using Validation.PackageSigning.ValidateCertificate;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    [DebuggerDisplay("{Certificate.Subject}")]
    public class CertificateInfo
    {
        public CertificateInfo(
            string sha256Base64Url,
            X509Certificate2 certificate,
            X509Certificate2Collection extraCertificates)
        {
            Fingerprint = sha256Base64Url;
            Certificate = certificate;
            Extra = extraCertificates;
        }

        public string Fingerprint { get; }
        public X509Certificate2 Certificate { get; }
        public X509Certificate2Collection Extra { get; }
        public Dictionary<EndCertificateUse, CertificateVerificationResult<ChainInfo>> Results { get; } = new();
        public Dictionary<PackageIdentity, ICatalogLeafItem> Packages { get; } = new();
        public CertificateInfo Issuer { get; set; }
    }
}
