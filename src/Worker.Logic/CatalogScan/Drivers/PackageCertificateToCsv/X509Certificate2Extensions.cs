// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public static class X509Certificate2Extensions
    {
        public static IReadOnlyList<X509ExtensionInfo> GetExtensions(this X509Certificate2 certificate)
        {
            return certificate
                .Extensions
                .Cast<X509Extension>()
                .Select(x => X509ExtensionInfoFactory.Create(x))
                .ToList();
        }

        public static IReadOnlyList<X509PolicyInfo>? GetPolicies(this X509Certificate2 certificate)
        {
            var certificatePolicy = certificate.Extensions[Oids.CertificatePolicies.Value!];

            if (certificatePolicy is not null)
            {
                var reader = new AsnReader(certificatePolicy.RawData, AsnEncodingRules.DER);
                var sequenceReader = reader.ReadSequence();
                reader.ThrowIfNotEmpty();

                var policies = new List<X509PolicyInfo>();

                while (sequenceReader.HasData)
                {
                    policies.Add(X509PolicyInfo.Read(sequenceReader));
                }

                return policies;
            }

            return null;
        }
    }
}
