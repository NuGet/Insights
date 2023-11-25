// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.CatalogDataToCsv
{
    public partial record CatalogLeafItemRecord : ICsvRecord
    {
        public CatalogLeafItemRecord()
        {
        }

        public CatalogLeafItemRecord(PackageDetailsCatalogLeaf leaf, string pageUrl) : this((CatalogLeaf)leaf, pageUrl)
        {
            IsListed = leaf.IsListed();
            Created = leaf.Created;
            LastEdited = leaf.LastEdited;
            Published = leaf.Published;
            PackageSize = leaf.PackageSize;
            PackageHash = leaf.PackageHash;
            PackageHashAlgorithm = leaf.PackageHashAlgorithm;
            Deprecation = KustoDynamicSerializer.Serialize(leaf.Deprecation);
            Vulnerabilities = KustoDynamicSerializer.Serialize(leaf.Vulnerabilities);
        }

        public CatalogLeafItemRecord(PackageDeleteCatalogLeaf leaf, string pageUrl) : this((CatalogLeaf)leaf, pageUrl)
        {
            Published = leaf.Published;
        }

        private CatalogLeafItemRecord(CatalogLeaf leaf, string pageUrl)
        {
            CommitId = leaf.CommitId;
            CommitTimestamp = leaf.CommitTimestamp;
            LowerId = leaf.PackageId.ToLowerInvariant();
            Identity = $"{LowerId}/{leaf.ParsePackageVersion().ToNormalizedString().ToLowerInvariant()}";
            Id = leaf.PackageId;
            Version = leaf.PackageVersion;
            Type = leaf.LeafType;
            Url = leaf.Url;
            PageUrl = pageUrl ?? throw new ArgumentNullException(nameof(pageUrl));
        }

        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public CatalogLeafType Type { get; set; }
        public string Url { get; set; }

        public string PageUrl { get; set; }

        public DateTimeOffset? Published { get; set; }

        public bool? IsListed { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? LastEdited { get; set; }
        public long? PackageSize { get; set; }
        public string PackageHash { get; set; }
        public string PackageHashAlgorithm { get; set; }

        [KustoType("dynamic")]
        public string Deprecation { get; set; }

        [KustoType("dynamic")]
        public string Vulnerabilities { get; set; }
    }
}
