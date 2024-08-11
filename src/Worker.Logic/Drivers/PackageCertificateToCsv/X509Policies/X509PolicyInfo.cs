// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Formats.Asn1;

#nullable enable

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    /// <summary>
    /// From RFC 5280 (https://www.rfc-editor.org/rfc/rfc5280#section-4.2.1.4):
    /// 
    ///     PolicyInformation ::= SEQUENCE {
    ///         policyIdentifier   CertPolicyId,
    ///         policyQualifiers   SEQUENCE SIZE (1..MAX) OF
    ///                                 PolicyQualifierInfo OPTIONAL }
    ///
    ///     CertPolicyId ::= OBJECT IDENTIFIER
    /// </summary>
    public class X509PolicyInfo
    {
        public X509PolicyInfo(string policyIdentifier, IReadOnlyList<X509PolicyQualifierInfo> policyQualifiers)
        {
            PolicyIdentifier = policyIdentifier;
            PolicyQualifiers = policyQualifiers;
        }

        public string PolicyIdentifier { get; }
        public IReadOnlyList<X509PolicyQualifierInfo> PolicyQualifiers { get; }

        public static X509PolicyInfo Read(AsnReader reader)
        {
            var policyInfoReader = reader.ReadSequence();
            var policyIdentifier = policyInfoReader.ReadObjectIdentifier();
            bool isAnyPolicy = policyIdentifier == Oids.AnyPolicy.Value;
            IReadOnlyList<X509PolicyQualifierInfo>? policyQualifiers = null;

            if (policyInfoReader.HasData)
            {
                policyQualifiers = ReadPolicyQualifiers(policyInfoReader, isAnyPolicy);
            }

            return new X509PolicyInfo(policyIdentifier, policyQualifiers ?? Array.Empty<X509PolicyQualifierInfo>());
        }

        private static IReadOnlyList<X509PolicyQualifierInfo> ReadPolicyQualifiers(
            AsnReader reader,
            bool isAnyPolicy)
        {
            var policyQualifiersReader = reader.ReadSequence();
            var policyQualifiers = new List<X509PolicyQualifierInfo>();

            while (policyQualifiersReader.HasData)
            {
                var policyQualifier = X509PolicyQualifierInfoFactory.Create(policyQualifiersReader);

                if (isAnyPolicy)
                {
                    if (policyQualifier.PolicyQualifierId != Oids.IdQtCps.Value &&
                        policyQualifier.PolicyQualifierId != Oids.IdQtUnotice.Value)
                    {
                        throw new InvalidOperationException("Invalid ASN.1: policy qualifier ID had an unexpected value: " + policyQualifier.PolicyQualifierId);
                    }
                }

                policyQualifiers.Add(policyQualifier);
            }

            if (policyQualifiers.Count == 0)
            {
                throw new InvalidOperationException("Invalid ASN.1: no policy qualifiers were found.");
            }

            return policyQualifiers;
        }
    }
}
