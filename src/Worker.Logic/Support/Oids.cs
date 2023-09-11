// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;

#nullable enable

namespace NuGet.Insights.Worker
{
    public static class Oids
    {
        public static readonly Oid BusinessCategory = new Oid(DottedDecimals.BusinessCategory);
        public static readonly Oid OrganizationIdentifier = new Oid(DottedDecimals.OrganizationIdentifier);

        public static readonly Oid JurisdictionLocalityName = new Oid(DottedDecimals.JurisdictionLocalityName);
        public static readonly Oid JurisdictionStateOrProvinceName = new Oid(DottedDecimals.JurisdictionStateOrProvinceName);
        public static readonly Oid JurisdictionCountryName = new Oid(DottedDecimals.JurisdictionCountryName);

        public static readonly Oid AnyPolicy = new(DottedDecimals.AnyPolicy);
        public static readonly Oid CertificatePolicies = new(DottedDecimals.CertificatePolicies);
        public static readonly Oid ExtendedValidationCodeSigning = new(DottedDecimals.ExtendedValidationCodeSigning);
        public static readonly Oid IdQtCps = new(DottedDecimals.IdQtCps);
        public static readonly Oid IdQtUnotice = new(DottedDecimals.IdQtUnotice);

        public class DottedDecimals
        {
            /// <summary>
            /// RFC 2256 "businessCategory" https://www.rfc-editor.org/rfc/rfc2256#section-5.16
            /// </summary>
            public const string BusinessCategory = "2.5.4.15";

            /// <summary>
            /// Source: https://github.com/openssl/openssl/blob/7303c5821779613e9a7fe239990662f80284a693/crypto/objects/objects.txt
            /// </summary>
            public const string OrganizationIdentifier = "2.5.4.97";

            /// <summary>
            /// Source: https://cabforum.org/wp-content/uploads/CA-Browser-Forum-EV-Guidelines-1.8.0.pdf, section 9.2.4
            /// </summary>
            public const string JurisdictionLocalityName = "1.3.6.1.4.1.311.60.2.1.1";

            /// <summary>
            /// Source: https://cabforum.org/wp-content/uploads/CA-Browser-Forum-EV-Guidelines-1.8.0.pdf, section 9.2.4
            /// </summary>
            public const string JurisdictionStateOrProvinceName = "1.3.6.1.4.1.311.60.2.1.2";

            /// <summary>
            /// Source: https://cabforum.org/wp-content/uploads/CA-Browser-Forum-EV-Guidelines-1.8.0.pdf, section 9.2.4
            /// </summary>
            public const string JurisdictionCountryName = "1.3.6.1.4.1.311.60.2.1.3";

            /// <summary>
            /// RFC 5280 "anyPolicy" https://www.rfc-editor.org/rfc/rfc5280#section-4.2.1.4
            /// </summary>
            public const string AnyPolicy = "2.5.29.32.0";

            /// <summary>
            /// RFC 5280 "id-ce-certificatePolicies" https://www.rfc-editor.org/rfc/rfc5280.html#section-4.2.1.4
            /// </summary>
            public const string CertificatePolicies = "2.5.29.32";

            /// <summary>
            /// CA/B Forum "extended-validation-codesigning" https://cabforum.org/object-registry/
            /// </summary>
            public const string ExtendedValidationCodeSigning = "2.23.140.1.3";

            /// <summary>
            /// RFC 5280 "id-qt-cps" https://www.rfc-editor.org/rfc/rfc5280.html#section-4.2.1.4
            /// </summary>
            public const string IdQtCps = "1.3.6.1.5.5.7.2.1";

            /// <summary>
            /// RFC 5280 "id-qt-unotice" https://www.rfc-editor.org/rfc/rfc5280.html#section-4.2.1.4
            /// </summary>
            public const string IdQtUnotice = "1.3.6.1.5.5.7.2.2";
        }
    }
}
