// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    [CsvRecord]
    public partial record PackageCertificateRecord : PackageRecord, IAggregatedCsvRecord<PackageCertificateRecord>
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

        public static string CsvCompactMessageSchemaName => "cc.pc";
        public static IEqualityComparer<PackageCertificateRecord> KeyComparer { get; } = PackageCertificateRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(Identity), nameof(Fingerprint)];

        public static List<PackageCertificateRecord> Prune(List<PackageCertificateRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
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

        public class PackageCertificateRecordKeyComparer : IEqualityComparer<PackageCertificateRecord>
        {
            public static PackageCertificateRecordKeyComparer Instance { get; } = new();

            public bool Equals(PackageCertificateRecord x, PackageCertificateRecord y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Identity == y.Identity
                    && x.Fingerprint == y.Fingerprint;
            }

            public int GetHashCode([DisallowNull] PackageCertificateRecord obj)
            {
                var hashCode = new HashCode();
                hashCode.Add(obj.Identity);
                hashCode.Add(obj.Fingerprint);
                return hashCode.ToHashCode();
            }
        }
    }
}
