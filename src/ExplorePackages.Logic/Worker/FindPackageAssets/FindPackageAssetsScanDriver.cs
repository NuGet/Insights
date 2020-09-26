using CsvHelper;
using CsvHelper.TypeConversion;
using Knapcode.ExplorePackages.Entities;
using Knapcode.MiniZip;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsScanDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ILogger<FindPackageAssetsScanDriver> _logger;

        public FindPackageAssetsScanDriver(
            CatalogClient catalogClient,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            ServiceClientFactory serviceClientFactory,
            ILogger<FindPackageAssetsScanDriver> logger)
        {
            _catalogClient = catalogClient;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _serviceClientFactory = serviceClientFactory;
            _logger = logger;
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

            var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.ParsedLeafType, leafScan.Url);

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

            var assets = GetAssets(leaf, files);
            if (assets.Count == 0)
            {
                // Ignore packages with no assets.
                return;
            }

            var storageAccount = _serviceClientFactory.GetStorageAccount();
            var client = storageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference("findpackageassets");
            var lowerId = leafScan.PackageId.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(leafScan.PackageVersion).ToNormalizedString().ToLowerInvariant();

            int bucket;
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes($"{lowerId}/{lowerVersion}"));
                bucket = (int)(BitConverter.ToUInt64(hash) % 1000);
            }

            var blob = container.GetAppendBlobReference($"{bucket}.csv");
            if (!await blob.ExistsAsync())
            {
                try
                {
                    await blob.CreateOrReplaceAsync(
                        accessCondition: AccessCondition.GenerateIfNotExistsCondition(),
                        options: null,
                        operationContext: null);
                }
                catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
                {
                    // Ignore this exception.
                }

                // Best effort, will not be re-executed on retry since the blob will exist at that point.
                blob.Properties.ContentType = "text/plain";
                await blob.SetPropertiesAsync();
            }

            using var writeMemoryStream = new MemoryStream();
            using (var streamWriter = new StreamWriter(writeMemoryStream, Encoding.UTF8))
            using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {
                var options = new TypeConverterOptions { Formats = new[] { "O" } };
                csvWriter.Configuration.TypeConverterOptionsCache.AddOptions<DateTimeOffset>(options);
                csvWriter.Configuration.HasHeaderRecord = false;
                csvWriter.WriteRecords(assets);
            }

            using var readMemoryStream = new MemoryStream(writeMemoryStream.ToArray());
            await blob.AppendBlockAsync(readMemoryStream);
        }

        private List<PackageAsset> GetAssets(PackageDetailsCatalogLeaf leaf, List<string> files)
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
                List<ContentItemGroup> groups;
                try
                {
                    // We must enumerate the item groups here to force the exception to be thrown, if any.
                    groups = contentItemCollection.FindItemGroups(pair.Value).ToList();
                }
                catch (ArgumentException ex) when (
                    ex.Message.StartsWith("Invalid portable frameworks '")
                    && ex.Message.EndsWith("'. A hyphen may not be in any of the portable framework names."))
                {
                    // Skip this package.
                    _logger.LogWarning(ex, "Package {Id} {Version} contains an invalid portable framework.", leaf.PackageId, packageVersion);
                    return new List<PackageAsset>();
                }

                foreach (var group in groups)
                {
                    string framework;
                    try
                    {
                        framework = ((NuGetFramework)group.Properties[ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker]).GetShortFolderName();
                    }
                    catch (FrameworkException ex) when (
                        ex.Message.StartsWith("Invalid portable frameworks for '")
                        && ex.Message.EndsWith("'. A portable framework must have at least one framework in the profile."))
                    {
                        // Skip this package.
                        _logger.LogWarning(ex, "Package {Id} {Version} contains an invalid portable framework.", leaf.PackageId, packageVersion);
                        return new List<PackageAsset>();
                    }

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
