using Knapcode.ExplorePackages.Entities;
using Knapcode.MiniZip;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using SqlBulkTools;
using SqlBulkTools.Enumeration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsScanDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly IOptions<ExplorePackagesSettings> _options;

        public FindPackageAssetsScanDriver(
            CatalogClient catalogClient,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            IOptions<ExplorePackagesSettings> options)
        {
            _catalogClient = catalogClient;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _options = options;
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.Expand);
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            return Task.FromResult(CatalogPageScanResult.Expand);
        }

        public async Task ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            if (leafScan.ParsedLeafType == CatalogLeafType.PackageDelete)
            {

                // Ignore delete events.
                return;
            }

            var catalogLeaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.ParsedLeafType, leafScan.Url);

            var flatContainerBaseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var normalizedVersion = NuGetVersion.Parse(leafScan.PackageVersion).ToNormalizedString();
            var url = _flatContainerClient.GetPackageContentUrl(flatContainerBaseUrl, leafScan.PackageId, leafScan.PackageVersion);

            List<string> files;
            try
            {
                using (var reader = await _httpZipProvider.GetReaderAsync(new Uri(url)))
                {
                    var zipDirectory = await reader.ReadAsync();
                    files = zipDirectory
                        .Entries
                        .Select(x => x.GetName())
                        .Select(x => x.Replace('\\', '/'))
                        .Distinct()
                        .ToList();
                }
            }
            catch (MiniZipHttpStatusCodeException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore deleted packages.
                return;
            }

            var assets = GetAssets(catalogLeaf, files);
            if (assets.Count == 0)
            {
                // Ignore packages with no assets.
                return;
            }

            var bulk = new BulkOperations();
            using (var connection = new SqlConnection(_options.Value.FindPackageAssetsSqlConnectionString))
            {
                bulk.Setup<PackageAsset>()
                    .ForCollection(assets)
                    .WithTable("PackageAssets")
                    .AddColumn(x => x.PackageAssetKey)
                    .AddColumn(x => x.Id)
                    .AddColumn(x => x.Version)
                    .AddColumn(x => x.Created)
                    .AddColumn(x => x.PatternSet)
                    .AddColumn(x => x.Framework)
                    .AddColumn(x => x.Path)
                    .BulkInsertOrUpdate()
                    .SetIdentityColumn(x => x.PackageAssetKey, ColumnDirectionType.InputOutput)
                    .MatchTargetOn(x => x.Id)
                    .MatchTargetOn(x => x.Version)
                    .MatchTargetOn(x => x.PatternSet)
                    .MatchTargetOn(x => x.Framework)
                    .MatchTargetOn(x => x.Path)
                    .Commit(connection);
            }
        }

        private static List<PackageAsset> GetAssets(PackageDetailsCatalogLeaf leaf, List<string> files)
        {
            var packageVersion = leaf.ParsePackageVersion().ToNormalizedString();

            var contentItemCollection = new ContentItemCollection();
            contentItemCollection.Load(files);

            var runtimeGraph = new RuntimeGraph();
            var conventions = new ManagedCodeConventions(runtimeGraph);

            var patternSets = conventions
                .Patterns
                .GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty)
                .Where(x => x.Name != nameof(ManagedCodeConventions.Patterns.AnyTargettedFile)) // This pattern is unused...
                .ToDictionary(x => x.Name, x => (PatternSet)x.GetGetMethod().Invoke(conventions.Patterns, null));

            var assets = new List<PackageAsset>();
            foreach (var pair in patternSets)
            {
                var groups = contentItemCollection.FindItemGroups(pair.Value);
                foreach (var group in groups)
                {
                    var framework = ((NuGetFramework)group.Properties[ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker]).GetShortFolderName();
                    foreach (var item in group.Items)
                    {
                        assets.Add(new PackageAsset
                        {
                            Id = leaf.PackageId,
                            Version = packageVersion,
                            Created = leaf.Created,
                            PatternSet = pair.Key,
                            Framework = framework,
                            Path = item.Path,
                        });
                    }
                }
            }

            return assets;
        }
    }
}
