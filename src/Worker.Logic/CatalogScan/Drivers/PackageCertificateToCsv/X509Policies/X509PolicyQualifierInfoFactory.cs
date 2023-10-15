// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Formats.Asn1;

#nullable enable

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public static class X509PolicyQualifierInfoFactory
    {
        public static X509PolicyQualifierInfo Create(AsnReader reader)
        {
            var policyQualifierReader = reader.ReadSequence();
            var policyQualifierId = policyQualifierReader.ReadObjectIdentifier();
            ReadOnlyMemory<byte> qualifierBytes = default;

            if (policyQualifierReader.HasData)
            {
                qualifierBytes = policyQualifierReader.ReadEncodedValue();

                policyQualifierReader.ThrowIfNotEmpty();
            }

            var qualifier = qualifierBytes.Span.ToBase64();

            if (policyQualifierId == Oids.IdQtCps.Value)
            {
                var qualifierReader = new AsnReader(qualifierBytes, AsnEncodingRules.DER);
                var cpsUri = qualifierReader.ReadCharacterString(UniversalTagNumber.IA5String);
                qualifierReader.ThrowIfNotEmpty();

                return new X509CpsPolicyQualifierInfo(policyQualifierId, qualifier, cpsUri);
            }

            return new X509PolicyQualifierInfo(policyQualifierId, qualifier, recognized: false);
        }
    }
}
