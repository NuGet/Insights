﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Signing;
using NuGet.RuntimeModel;

namespace Knapcode.ExplorePackages.Worker.FindPackageSignature
{
    public class FindPackageSignatureDriver : ICatalogLeafToCsvDriver<PackageSignature>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<FindPackageSignatureDriver> _logger;

        public FindPackageSignatureDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<FindPackageSignatureDriver> logger)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _options = options;
            _logger = logger;
        }

        public string ResultsContainerName => _options.Value.PackageSignatureContainerName;

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

            output.AuthorTimestampSHA1 = info.TimestampSHA1;
            output.AuthorTimestampSHA256 = info.TimestampSHA256;
            output.AuthorTimestampSubject = info.TimestampSubject;
            output.AuthorTimestampNotBefore = info.TimestampNotBefore;
            output.AuthorTimestampNotAfter = info.TimestampNotAfter;
            output.AuthorTimestampValue = info.TimestampValue;
        }
        private void ApplyRepositorySignature<T>(PackageSignature output, T signature) where T : Signature, IRepositorySignature
        {
            var info = GetInfo(signature);

            output.PackageOwners = JsonConvert.SerializeObject(signature.PackageOwners);

            output.RepositorySHA1 = info.SHA1;
            output.RepositorySHA256 = info.SHA256;
            output.RepositorySubject = info.Subject;
            output.RepositoryNotBefore = info.NotBefore;
            output.RepositoryNotAfter = info.NotAfter;

            output.RepositoryTimestampSHA1 = info.TimestampSHA1;
            output.RepositoryTimestampSHA256 = info.TimestampSHA256;
            output.RepositoryTimestampSubject = info.TimestampSubject;
            output.RepositoryTimestampNotBefore = info.TimestampNotBefore;
            output.RepositoryTimestampNotAfter = info.TimestampNotAfter;
            output.RepositoryTimestampValue = info.TimestampValue;
        }

        private SignatureInfo GetInfo(Signature signature)
        {
            var timestamp = signature.Timestamps.Single();

            return new SignatureInfo
            {
                SHA1 = signature.SignerInfo.Certificate.Thumbprint,
                SHA256 = CertificateUtility.GetHashString(signature.SignerInfo.Certificate, HashAlgorithmName.SHA256),
                Subject = signature.SignerInfo.Certificate.Subject,
                NotBefore = signature.SignerInfo.Certificate.NotBefore.ToUniversalTime(),
                NotAfter = signature.SignerInfo.Certificate.NotAfter.ToUniversalTime(),

                TimestampSHA1 = timestamp.SignerInfo.Certificate.Thumbprint,
                TimestampSHA256 = CertificateUtility.GetHashString(timestamp.SignerInfo.Certificate, HashAlgorithmName.SHA256),
                TimestampSubject = timestamp.SignerInfo.Certificate.Subject,
                TimestampNotBefore = timestamp.SignerInfo.Certificate.NotBefore.ToUniversalTime(),
                TimestampNotAfter = timestamp.SignerInfo.Certificate.NotAfter.ToUniversalTime(),
                TimestampValue = timestamp.GeneralizedTime.ToUniversalTime(),
            };
        }

        private record SignatureInfo
        {
            public string SHA1 { get; init; }
            public string SHA256 { get; init; }
            public string Subject { get; init; }
            public DateTimeOffset NotBefore { get; init; }
            public DateTimeOffset NotAfter { get; init; }

            public string TimestampSHA1 { get; init; }
            public string TimestampSHA256 { get; init; }
            public string TimestampSubject { get; init; }
            public DateTimeOffset TimestampNotBefore { get; init; }
            public DateTimeOffset TimestampNotAfter { get; init; }

            public DateTimeOffset TimestampValue { get; init; }
        }
    }
}