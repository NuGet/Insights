// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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

        public List<PackageSignature> Prune(List<PackageSignature> records)
        {
            return PackageRecord.Prune(records);
        }

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
        }

        public async Task<DriverResult<CsvRecordSet<PackageSignature>>> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
        {
            var records = await ProcessLeafInternalAsync(item);
            return DriverResult.Success(new CsvRecordSet<PackageSignature>(PackageRecord.GetBucketKey(item), records));
        }

        private async Task<List<PackageSignature>> ProcessLeafInternalAsync(CatalogLeafItem item)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                return new List<PackageSignature> { new PackageSignature(scanId, scanTimestamp, leaf) };
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                var primarySignature = await _packageFileService.GetPrimarySignatureAsync(item);
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
                Subject = FixDistinguishedName(signature.SignerInfo.Certificate.Subject),
                NotBefore = signature.SignerInfo.Certificate.NotBefore.ToUniversalTime(),
                NotAfter = signature.SignerInfo.Certificate.NotAfter.ToUniversalTime(),
                Issuer = FixDistinguishedName(signature.SignerInfo.Certificate.Issuer),

                TimestampSHA1 = timestamp?.SignerInfo.Certificate.Thumbprint,
                TimestampSHA256 = timestamp != null ? CertificateUtility.GetHashString(timestamp.SignerInfo.Certificate, NuGet.Common.HashAlgorithmName.SHA256) : null,
                TimestampSubject = FixDistinguishedName(timestamp?.SignerInfo.Certificate.Subject),
                TimestampNotBefore = timestamp?.SignerInfo.Certificate.NotBefore.ToUniversalTime(),
                TimestampNotAfter = timestamp?.SignerInfo.Certificate.NotAfter.ToUniversalTime(),
                TimestampIssuer = FixDistinguishedName(timestamp?.SignerInfo.Certificate.Issuer),
                TimestampValue = timestamp?.GeneralizedTime.ToUniversalTime(),
            };
        }

        /// <summary>
        /// Use to bring OID parsing on Windows up to parity with OpenSSL. This is not exhaustive but is based on OIDs
        /// found in NuGet package signatures on NuGet.org. The purpose of this conversation is so that CSV output is
        /// the same no matter the platform that's running the driver.
        /// </summary>
        private static readonly IReadOnlyDictionary<Regex, string> OidReplacements = new Dictionary<string, string>
        {
            // Source: https://github.com/openssl/openssl/blob/7303c5821779613e9a7fe239990662f80284a693/crypto/objects/objects.txt
            { "2.5.4.15", "businessCategory" },
            { "2.5.4.97", "organizationIdentifier" },
            { "1.3.6.1.4.1.311.60.2.1.1", "jurisdictionLocalityName" },
            { "1.3.6.1.4.1.311.60.2.1.2", "jurisdictionStateOrProvinceName" },
            { "1.3.6.1.4.1.311.60.2.1.3", "jurisdictionCountryName" },
        }.ToDictionary(x => new Regex(@$"(^|, )OID\.{Regex.Escape(x.Key)}="), x => @$"$1{x.Value}=");

        private static string FixDistinguishedName(string name)
        {
            if (name is null)
            {
                return null;
            }

            foreach (var pair in OidReplacements)
            {
                name = pair.Key.Replace(name, pair.Value);
            }

            return name;
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(PackageSignature record)
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
