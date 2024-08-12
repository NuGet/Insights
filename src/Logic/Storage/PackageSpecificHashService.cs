// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;

#nullable enable

namespace NuGet.Insights
{
    public abstract class PackageSpecificHashService
    {
        private readonly PackageWideEntityService _packageWideEntityService;

        public PackageSpecificHashService(PackageWideEntityService packageWideEntityService)
        {
            _packageWideEntityService = packageWideEntityService;
        }

        protected abstract string TableName { get; }
        protected abstract bool MissingHashesIsDeleted { get; }

        public async Task InitializeAsync()
        {
            await _packageWideEntityService.InitializeAsync(TableName);
        }

        public async Task DeleteTableAsync()
        {
            await _packageWideEntityService.DeleteTableAsync(TableName);
        }

        public async Task SetHashesAsync(IPackageIdentityCommit item, ILookup<string, string>? headers, HashOutput? hashes)
        {
            if (item.LeafType == CatalogLeafType.PackageDelete && hashes is not null)
            {
                throw new ArgumentException("The hashes must be null when the leaf type is a delete.");
            }

            if (MissingHashesIsDeleted && item.LeafType != CatalogLeafType.PackageDetails && hashes is not null)
            {
                throw new ArgumentException("The hashes must not be null when the leaf type is not a delete.");
            }

            if ((headers is null) != (hashes is null))
            {
                throw new ArgumentException("If the hashes are provided, the headers must also be provided.");
            }

            await _packageWideEntityService.UpdateBatchAsync(
                TableName,
                item.PackageId,
                [item],
                item =>
                {
                    return Task.FromResult(new HashInfoV1
                    {
                        Available = hashes is not null,
                        CommitTimestamp = item.CommitTimestamp,
                        HttpHeaders = headers,
                        MD5 = hashes?.MD5,
                        SHA1 = hashes?.SHA1,
                        SHA256 = hashes?.SHA256,
                        SHA512 = hashes?.SHA512,
                    });
                },
                OutputToData,
                DataToOutput);
        }

        public async Task<HashOutput?> GetHashesAsync(IPackageIdentityCommit item, bool requireFresh)
        {
            var info = await _packageWideEntityService.GetInfoAsync<IPackageIdentityCommit, HashInfoVersions, HashInfoV1>(
                TableName,
                item,
                DataToOutput,
                requireFresh);

            if (info is null || !info.Available)
            {
                return null;
            }

            return new HashOutput
            {
                MD5 = info.MD5!,
                SHA1 = info.SHA1!,
                SHA256 = info.SHA256!,
                SHA512 = info.SHA512!,
            };
        }

        private static HashInfoV1 DataToOutput(HashInfoVersions data)
        {
            return data.V1;
        }

        private static HashInfoVersions OutputToData(HashInfoV1 output)
        {
            return new HashInfoVersions(output);
        }

        [MessagePackObject]
        public class HashInfoVersions : PackageWideEntityService.IPackageWideEntity
        {
            [SerializationConstructor]
            public HashInfoVersions(HashInfoV1 v1)
            {
                V1 = v1;
            }

            [Key(0)]
            public HashInfoV1 V1 { get; set; }

            DateTimeOffset? PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class HashInfoV1
        {
            [Key(1)]
            public DateTimeOffset? CommitTimestamp { get; set; }

            [Key(2)]
            public bool Available { get; set; }

            [Key(3)]
            public ILookup<string, string>? HttpHeaders { get; set; }

            [Key(4)]
            public byte[]? MD5 { get; set; }

            [Key(5)]
            public byte[]? SHA1 { get; set; }

            [Key(6)]
            public byte[]? SHA256 { get; set; }

            [Key(7)]
            public byte[]? SHA512 { get; set; }
        }
    }
}
