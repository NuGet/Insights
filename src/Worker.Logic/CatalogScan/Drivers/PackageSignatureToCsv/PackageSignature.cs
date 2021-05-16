using System;
using NuGet.Common;

namespace NuGet.Insights.Worker.PackageSignatureToCsv
{
    public partial record PackageSignature : PackageRecord, ICsvRecord
    {
        public PackageSignature()
        {
        }

        public PackageSignature(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageSignatureResultType.Deleted;
        }

        public PackageSignature(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageSignatureResultType.Available;
        }

        public PackageSignatureResultType ResultType { get; set; }

        public HashAlgorithmName HashAlgorithm { get; set; }
        public string HashValue { get; set; }

        public string AuthorSHA1 { get; set; }
        public string AuthorSHA256 { get; set; }
        public string AuthorSubject { get; set; }
        public DateTimeOffset? AuthorNotBefore { get; set; }
        public DateTimeOffset? AuthorNotAfter { get; set; }
        public string AuthorIssuer { get; set; }

        public string AuthorTimestampSHA1 { get; set; }
        public string AuthorTimestampSHA256 { get; set; }
        public string AuthorTimestampSubject { get; set; }
        public DateTimeOffset? AuthorTimestampNotBefore { get; set; }
        public DateTimeOffset? AuthorTimestampNotAfter { get; set; }
        public string AuthorTimestampIssuer { get; set; }
        public DateTimeOffset? AuthorTimestampValue { get; set; }
        public bool AuthorTimestampHasASN1Error { get; set; }

        public string RepositorySHA1 { get; set; }
        public string RepositorySHA256 { get; set; }
        public string RepositorySubject { get; set; }
        public DateTimeOffset? RepositoryNotBefore { get; set; }
        public DateTimeOffset? RepositoryNotAfter { get; set; }
        public string RepositoryIssuer { get; set; }

        public string RepositoryTimestampSHA1 { get; set; }
        public string RepositoryTimestampSHA256 { get; set; }
        public string RepositoryTimestampSubject { get; set; }
        public DateTimeOffset? RepositoryTimestampNotBefore { get; set; }
        public DateTimeOffset? RepositoryTimestampNotAfter { get; set; }
        public string RepositoryTimestampIssuer { get; set; }
        public DateTimeOffset? RepositoryTimestampValue { get; set; }
        public bool RepositoryTimestampHasASN1Error { get; set; }

        [KustoType("dynamic")]
        public string PackageOwners { get; set; }
    }
}
