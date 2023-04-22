// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Packaging.Signing;

namespace NuGet.Insights.Worker.PackageSignatureToCsv
{
    public class PackageSignatureToCsvDriver : ICatalogLeafToCsvDriver<PackageSignature>, ICsvResultStorage<PackageSignature>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageSignatureToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _options = options;
        }

        public string ResultContainerName => _options.Value.PackageSignatureContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageSignature> Prune(List<PackageSignature> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
        }
        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSet<PackageSignature>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var records = await ProcessLeafInternalAsync(leafScan);
            return DriverResult.Success(new CsvRecordSet<PackageSignature>(PackageRecord.GetBucketKey(leafScan), records));
        }

        private async Task<List<PackageSignature>> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return new List<PackageSignature> { new PackageSignature(scanId, scanTimestamp, leaf) };
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var primarySignature = await _packageFileService.GetPrimarySignatureAsync(leafScan);
                if (primarySignature == null)
                {
                    // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted record.
                    return new List<PackageSignature>();
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

                return new List<PackageSignature> { output };
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
            output.PackageOwners = JsonSerializer.Serialize(signature.PackageOwners);
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
            catch (CryptographicException ex) when (ex.IsInvalidDataException())
            {
                timestamp = null;
                timestampHasASN1Error = true;
            }

            return new SignatureInfo
            {
                TimestampHasASN1Error = timestampHasASN1Error,

                SHA1 = signature.SignerInfo.Certificate.GetSHA1HexFingerprint(),
                SHA256 = signature.SignerInfo.Certificate.GetSHA256HexFingerprint(),
                Subject = signature.SignerInfo.Certificate.GetSubjectXplat(),
                NotBefore = signature.SignerInfo.Certificate.NotBefore.ToUniversalTime(),
                NotAfter = signature.SignerInfo.Certificate.NotAfter.ToUniversalTime(),
                Issuer = signature.SignerInfo.Certificate.GetIssuerXplat(),

                TimestampSHA1 = timestamp?.SignerInfo.Certificate.GetSHA1HexFingerprint(),
                TimestampSHA256 = timestamp != null ? timestamp.SignerInfo.Certificate.GetSHA256HexFingerprint() : null,
                TimestampSubject = timestamp?.SignerInfo.Certificate.GetSubjectXplat(),
                TimestampNotBefore = timestamp?.SignerInfo.Certificate.NotBefore.ToUniversalTime(),
                TimestampNotAfter = timestamp?.SignerInfo.Certificate.NotAfter.ToUniversalTime(),
                TimestampIssuer = timestamp?.SignerInfo.Certificate.GetIssuerXplat(),
                TimestampValue = timestamp?.GeneralizedTime.ToUniversalTime(),
            };
        }

        public Task<(ICatalogLeafItem LeafItem, string PageUrl)> MakeReprocessItemOrNullAsync(PackageSignature record)
        {
            throw new NotImplementedException();
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
