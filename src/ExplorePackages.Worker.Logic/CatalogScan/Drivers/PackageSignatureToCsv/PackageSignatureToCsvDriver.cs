using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Packaging.Signing;

namespace Knapcode.ExplorePackages.Worker.PackageSignatureToCsv
{
    public class PackageSignatureToCsvDriver : ICatalogLeafToCsvDriver<PackageSignature>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<PackageSignatureToCsvDriver> _logger;

        public PackageSignatureToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<PackageSignatureToCsvDriver> logger)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _options = options;
            _logger = logger;
        }

        public string ResultsContainerName => _options.Value.PackageSignatureContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageSignature> Prune(List<PackageSignature> records)
        {
            return PackageRecord.Prune(records);
        }

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
        }

        public async Task<DriverResult<List<PackageSignature>>> ProcessLeafAsync(CatalogLeafItem item)
        {
            Guid? scanId = null;
            DateTimeOffset? scanTimestamp = null;
            if (_options.Value.AppendResultUniqueIds)
            {
                scanId = Guid.NewGuid();
                scanTimestamp = DateTimeOffset.UtcNow;
            }

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                return DriverResult.Success(new List<PackageSignature> { new PackageSignature(scanId, scanTimestamp, leaf) });
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                var primarySignature = await _packageFileService.GetPrimarySignatureAsync(item);
                if (primarySignature == null)
                {
                    // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted record.
                    return DriverResult.Success(new List<PackageSignature>());
                }

                var output = new PackageSignature(scanId, scanTimestamp, leaf)
                {
                    HashAlgorithm = primarySignature.SignatureContent.HashAlgorithm,
                    HashValue = primarySignature.SignatureContent.HashValue,
                };

                if (primarySignature.Type == SignatureType.Author)
                {
                    ApplyAuthorSignature(output, primarySignature);
                    ApplyRepositorySignature(output, RepositoryCountersignature.GetRepositoryCountersignature(primarySignature));
                }
                else if (primarySignature.Type == SignatureType.Repository)
                {
                    ApplyRepositorySignature(output, (RepositoryPrimarySignature)primarySignature);
                }
                else
                {
                    throw new NotSupportedException();
                }

                return DriverResult.Success(new List<PackageSignature> { output });
            }
        }

        private void ApplyAuthorSignature(PackageSignature output, Signature signature)
        {
            var info = GetInfo(signature);

            output.AuthorSHA1 = info.SHA1;
            output.AuthorSHA256 = info.SHA256;
            output.AuthorSubject = info.Subject;
            output.AuthorNotBefore = info.NotBefore;
            output.AuthorNotAfter = info.NotAfter;
            output.AuthorIssuer = info.Issuer;

            output.AuthorTimestampSHA1 = info.TimestampSHA1;
            output.AuthorTimestampSHA256 = info.TimestampSHA256;
            output.AuthorTimestampSubject = info.TimestampSubject;
            output.AuthorTimestampNotBefore = info.TimestampNotBefore;
            output.AuthorTimestampNotAfter = info.TimestampNotAfter;
            output.AuthorTimestampIssuer = info.TimestampIssuer;
            output.AuthorTimestampValue = info.TimestampValue;
            output.AuthorTimestampHasASN1Error = info.TimestampHasASN1Error;
        }

        private void ApplyRepositorySignature<T>(PackageSignature output, T signature) where T : Signature, IRepositorySignature
        {
            var info = GetInfo(signature);

            output.RepositorySHA1 = info.SHA1;
            output.RepositorySHA256 = info.SHA256;
            output.RepositorySubject = info.Subject;
            output.RepositoryNotBefore = info.NotBefore;
            output.RepositoryNotAfter = info.NotAfter;
            output.RepositoryIssuer = info.Issuer;

            output.RepositoryTimestampSHA1 = info.TimestampSHA1;
            output.RepositoryTimestampSHA256 = info.TimestampSHA256;
            output.RepositoryTimestampSubject = info.TimestampSubject;
            output.RepositoryTimestampNotBefore = info.TimestampNotBefore;
            output.RepositoryTimestampNotAfter = info.TimestampNotAfter;
            output.RepositoryTimestampIssuer = info.TimestampIssuer;
            output.RepositoryTimestampValue = info.TimestampValue;
            output.RepositoryTimestampHasASN1Error = info.TimestampHasASN1Error;
            output.PackageOwners = JsonConvert.SerializeObject(signature.PackageOwners);
        }

        private SignatureInfo GetInfo(Signature signature)
        {
            Timestamp timestamp;
            bool timestampHasASN1Error;
            try
            {
                timestamp = signature.Timestamps.Single();
                timestampHasASN1Error = false;
            }
            catch (CryptographicException ex) when (ex.Message == "The ASN.1 data is invalid.")
            {
                timestamp = null;
                timestampHasASN1Error = true;
            }

            return new SignatureInfo
            {
                TimestampHasASN1Error = timestampHasASN1Error,

                SHA1 = signature.SignerInfo.Certificate.Thumbprint,
                SHA256 = CertificateUtility.GetHashString(signature.SignerInfo.Certificate, NuGet.Common.HashAlgorithmName.SHA256),
                Subject = signature.SignerInfo.Certificate.Subject,
                NotBefore = signature.SignerInfo.Certificate.NotBefore.ToUniversalTime(),
                NotAfter = signature.SignerInfo.Certificate.NotAfter.ToUniversalTime(),
                Issuer = signature.SignerInfo.Certificate.Issuer,

                TimestampSHA1 = timestamp?.SignerInfo.Certificate.Thumbprint,
                TimestampSHA256 = timestamp != null ? CertificateUtility.GetHashString(timestamp.SignerInfo.Certificate, NuGet.Common.HashAlgorithmName.SHA256) : null,
                TimestampSubject = timestamp?.SignerInfo.Certificate.Subject,
                TimestampNotBefore = timestamp?.SignerInfo.Certificate.NotBefore.ToUniversalTime(),
                TimestampNotAfter = timestamp?.SignerInfo.Certificate.NotAfter.ToUniversalTime(),
                TimestampIssuer = timestamp?.SignerInfo.Certificate.Issuer,
                TimestampValue = timestamp?.GeneralizedTime.ToUniversalTime(),
            };
        }

        public string GetBucketKey(CatalogLeafItem item)
        {
            return PackageRecord.GetBucketKey(item);
        }

        private record SignatureInfo
        {
            public bool TimestampHasASN1Error { get; init; }

            public string SHA1 { get; init; }
            public string SHA256 { get; init; }
            public string Subject { get; init; }
            public DateTimeOffset NotBefore { get; init; }
            public DateTimeOffset NotAfter { get; init; }
            public string Issuer { get; init; }

            public string TimestampSHA1 { get; init; }
            public string TimestampSHA256 { get; init; }
            public string TimestampSubject { get; init; }
            public DateTimeOffset? TimestampNotBefore { get; init; }
            public DateTimeOffset? TimestampNotAfter { get; init; }
            public string TimestampIssuer { get; init; }
            public DateTimeOffset? TimestampValue { get; init; }
        }
    }
}
