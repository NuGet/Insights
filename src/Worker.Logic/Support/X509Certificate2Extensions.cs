// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using NuGet.Packaging.Signing;

#nullable enable

namespace NuGet.Insights.Worker
{
    public static class X509Certificate2Extensions
    {
        /// <summary>
        /// Produces a URL-safe base64 encoded SHA-256 fingerprint (thumbprint) for the provided certificate. This
        /// representation is ideal for Azure Table Storage keys.
        /// </summary>
        /// <param name="certificate">The certificate to get the thumbprint for.</param>
        /// <returns>The fingerprint.</returns>
        public static string GetSHA256Base64UrlFingerprint(this X509Certificate2 certificate)
        {
            return Base64UrlEncoder.Encode(CertificateUtility.GetHash(certificate, Common.HashAlgorithmName.SHA256));
        }

        public static string GetSHA256HexFingerprint(this X509Certificate2 certificate)
        {
            return CertificateUtility.GetHash(certificate, Common.HashAlgorithmName.SHA256).ToUpperHex();
        }

        public static string GetSHA1HexFingerprint(this X509Certificate2 certificate)
        {
            return certificate.Thumbprint;
        }

        public static string? GetSubjectXplat(this X509Certificate2 certificate)
        {
            return FixDistinguishedName(certificate.Subject);
        }

        public static string? GetIssuerXplat(this X509Certificate2 certificate)
        {
            return FixDistinguishedName(certificate.Issuer);
        }

        /// <summary>
        /// Use to bring OID parsing on Windows up to parity with OpenSSL. This is not exhaustive but is based on OIDs
        /// found in NuGet package signatures on NuGet.org. The purpose of this conversation is so that CSV output is
        /// the same no matter the platform that's running the driver.
        /// </summary>
        private static readonly IReadOnlyDictionary<Regex, string> OidReplacements = new Dictionary<string, string>
        {
            // Source: https://github.com/openssl/openssl/blob/7303c5821779613e9a7fe239990662f80284a693/crypto/objects/objects.txt
            { Oids.DottedDecimals.BusinessCategory, "businessCategory" },
            { Oids.DottedDecimals.OrganizationIdentifier, "organizationIdentifier" },
            { Oids.DottedDecimals.JurisdictionLocalityName, "jurisdictionLocalityName" },
            { Oids.DottedDecimals.JurisdictionStateOrProvinceName, "jurisdictionStateOrProvinceName" },
            { Oids.DottedDecimals.JurisdictionCountryName, "jurisdictionCountryName" },
        }.ToDictionary(x => new Regex(@$"(^|, )OID\.{Regex.Escape(x.Key)}="), x => @$"$1{x.Value}=");

        private static string? FixDistinguishedName(string name)
        {
            if (name is null)
            {
                return null;
            }

            foreach (var pair in OidReplacements)
            {
                name = pair.Key.Replace(name, pair.Value);
            }

            return name;
        }
    }
}
