// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public partial record PackageCertificateRecord : PackageRecord, ICsvRecord
    {
        public PackageCertificateRecord()
        {
        }

        public PackageCertificateRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageCertificateResultType.Deleted;
        }

        public PackageCertificateRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageCertificateResultType.Available;
        }

        public PackageCertificateResultType ResultType { get; set; }

        /// <summary>
        /// SHA-256, base64 URL encoded fingerprint of the certificate.
        /// </summary>
        public string Fingerprint { get; set; }

        public CertificateRelationshipTypes RelationshipTypes { get; set; }
    }
}
