﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Validation.PackageSigning.ValidateCertificate
{
    /// <summary>
    /// Verifies <see cref="X509Certificate2"/>.
    /// </summary>
    public interface ICertificateVerifier
    {
        /// <summary>
        /// Determine the status of a <see cref="X509Certificate2"/>.
        /// </summary>
        /// <param name="certificate">The certificate to verify.</param>
        /// <param name="extraCertificates">A collection of certificates that may be used to build the certificate chain.</param>
        /// <returns>The result of the verification.</returns>
        [Obsolete("This will be removed when integration tests are created")]
        CertificateVerificationResult<T> VerifyCertificate<T>(X509Certificate2 certificate, X509Certificate2[] extraCertificates, Func<X509Chain, T> getChainInfo);

        /// <summary>
        /// Determine the status of a code signing <see cref="X509Certificate2"/>.
        /// </summary>
        /// <param name="certificate">The certificate to verify.</param>
        /// <param name="extraCertificates">A collection of certificates that may be used to build the certificate chain.</param>
        /// <returns>The result of the verification.</returns>
        CertificateVerificationResult<T> VerifyCodeSigningCertificate<T>(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> extraCertificates, Func<X509Chain, T> getChainInfo);

        /// <summary>
        /// Determine the status of a timestamping <see cref="X509Certificate2"/>.
        /// </summary>
        /// <param name="certificate">The certificate to verify.</param>
        /// <param name="extraCertificates">A collection of certificates that may be used to build the certificate chain.</param>
        /// <returns>The result of the verification.</returns>
        CertificateVerificationResult<T> VerifyTimestampingCertificate<T>(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> extraCertificates, Func<X509Chain, T> getChainInfo);
    }
}
