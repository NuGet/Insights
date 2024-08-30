// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public partial record PackageCertificateRecord : PackageRecord, ICsvRecord, IAggregatedCsvRecord<PackageCertificateRecord>
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

        [Required]
        public PackageCertificateResultType ResultType { get; set; }

        /// <summary>
        /// SHA-256, base64 URL encoded fingerprint of the certificate.
        /// </summary>
        public string Fingerprint { get; set; }

        public CertificateRelationshipTypes? RelationshipTypes { get; set; }

        public static List<PackageCertificateRecord> Prune(List<PackageCertificateRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageCertificateRecord other)
        {
            var c = base.CompareTo(other);
            if (c != 0)
            {
                return c;
            }

            return string.CompareOrdinal(Fingerprint, other.Fingerprint);
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}
