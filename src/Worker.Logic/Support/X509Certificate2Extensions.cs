// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using NuGet.Packaging.Signing;

namespace NuGet.Insights.Worker
{
    public static class X509Certificate2Extensions
    {
        public static string GetSHA256HexFingerprint(this X509Certificate2 certificate)
        {
            return CertificateUtility.GetHash(certificate, Common.HashAlgorithmName.SHA256).ToUpperHex();
        }

        public static string GetSHA1HexFingerprint(this X509Certificate2 certificate)
        {
            return certificate.Thumbprint;
        }

        public static string GetSubjectXplat(this X509Certificate2 certificate)
        {
            return FixDistinguishedName(certificate.Subject);
        }

        public static string GetIssuerXplat(this X509Certificate2 certificate)
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
            { "2.5.4.15", "businessCategory" },
            { "2.5.4.97", "organizationIdentifier" },
            { "1.3.6.1.4.1.311.60.2.1.1", "jurisdictionLocalityName" },
            { "1.3.6.1.4.1.311.60.2.1.2", "jurisdictionStateOrProvinceName" },
            { "1.3.6.1.4.1.311.60.2.1.3", "jurisdictionCountryName" },
        }.ToDictionary(x => new Regex(@$"(^|, )OID\.{Regex.Escape(x.Key)}="), x => @$"$1{x.Value}=");

        private static string FixDistinguishedName(string name)
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
