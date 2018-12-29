using System;
using System.Net.Http;
using System.Threading.Tasks;
using Knapcode.BlobDelta;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages.Logic
{
    public class BlobStorageMigrator
    {
        private readonly PackageBlobNameProvider _nameProvider;
        private readonly IPackageService _packageService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _settings;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageMigrator(
            PackageBlobNameProvider nameProvider,
            IPackageService packageService,
            IFileStorageService fileStorageService,
            IOptionsSnapshot<ExplorePackagesSettings> settings,
            ILogger<BlobStorageService> logger)
        {
            _nameProvider = nameProvider;
            _packageService = packageService;
            _fileStorageService = fileStorageService;
            _settings = settings;
            _logger = logger;
        }

        public async Task MigrateAsync(BlobStorageMigrationSource source)
        {
            // Initialize the enumeration logic
            var leftContainer = GetSourceContainer(source);
            var leftEnumerable = new BlobEnumerable(leftContainer);

            var rightContainer = GetDestinationContainer();
            var rightEnumerable = new BlobEnumerable(rightContainer);

            var comparisonEnumerable = new MigrateToLatestComparisonEnumerable(
                leftEnumerable,
                rightEnumerable,
                NormalizeName);
            var comparisonEnumerator = comparisonEnumerable.GetEnumerator();

            // Detect each blob difference, one after another.
            while (await comparisonEnumerator.MoveNextAsync())
            {
                var comparison = comparisonEnumerator.Current;
                switch (comparison.Type)
                {
                    case BlobComparisonType.DifferentContent:
                    case BlobComparisonType.MissingFromLeft:
                    case BlobComparisonType.Same:
                        continue;
                    case BlobComparisonType.MissingFromRight:
                        await MigrateBlobAsync(comparison);
                        break;
                    default:
                        _logger.LogInformation(
                            "Unsupported blob comparison type '{Type}', source URL: {SourceUrl}, destination URL: {DestinationUrl}",
                            comparison.Type,
                            comparison.Left?.Blob.Uri.AbsoluteUri,
                            comparison.Right?.Blob.Uri.AbsoluteUri);
                        throw new NotSupportedException();
                }
            }
        }

        private string NormalizeName(string name)
        {
            if (_nameProvider.TryParseLatestBlobName(name, _logger, out var parsed))
            {
                return parsed.Canonical;
            }
            else
            {
                return name;
            }
        }

        private async Task MigrateBlobAsync(BlobComparison comparison)
        {
            var originalName = comparison.Left.Blob.Name;
            if (!_nameProvider.TryParseLatestBlobName(originalName, _logger, out var parsedName))
            {
                _logger.LogError("Could not migrate a blob name with an unexpected format: {Original}", originalName);
                return;
            }

            if (parsedName.IsCanonical)
            {
                _logger.LogInformation("Missing from right: {Name}", parsedName.Canonical);
            }
            else
            {
                _logger.LogInformation("Missing from right: {Name} (originally {Original})", parsedName.Canonical, parsedName.Original);
            }

            var package = await _packageService.GetPackageOrNullAsync(parsedName.Id, parsedName.Version);

            if (!package.CatalogPackage.Deleted)
            {
                _logger.LogError(
                    "Could not migrate a file related to a non-deleted package: {Id} {Version} {Type} ({Original})",
                    parsedName.Id,
                    parsedName.Version,
                    parsedName.Type,
                    parsedName.Original);
                return;
            }

            var leftBlob = comparison.Left.Blob as CloudBlockBlob;
            if (leftBlob == null)
            {
                _logger.LogError(
                    "Could not migrate a blob that is not a block blob: {Id} {Version} {Type} ({Original})",
                    parsedName.Id,
                    parsedName.Version,
                    parsedName.Type,
                    parsedName.Original);
                return;
            }

            await _fileStorageService.StoreStreamAsync(
                parsedName.Id,
                parsedName.Version,
                parsedName.Type,
                async destinationStream =>
                {
                    BlobStorageService.LogRequest(_logger, HttpMethod.Get, leftBlob);
                    using (var sourceStream = await leftBlob.OpenReadAsync(
                        accessCondition: null,
                        options: null,
                        operationContext: BlobStorageService.GetLoggingOperationContext(_logger)))
                    {
                        await sourceStream.CopyToAsync(destinationStream);
                    }
                },
                accessCondition: AccessCondition.GenerateIfNotExistsCondition());

            _logger.LogInformation(
                "Done migrating blob {Id} {Version} {Type} ({Canonical}).",
                parsedName.Id,
                parsedName.Version,
                parsedName.Type,
                parsedName.Canonical);
        }

        private CloudBlobContainer GetDestinationContainer()
        {
            var account = CloudStorageAccount.Parse(_settings.Value.StorageConnectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(_settings.Value.StorageContainerName);
            return container;
        }

        private CloudStorageAccount GetSourceAccount(BlobStorageMigrationSource source)
        {
            return new CloudStorageAccount(
                new StorageCredentials(source.SasToken),
                source.AccountName,
                source.EndpointSuffix,
                useHttps: true);
        }

        private CloudBlobContainer GetSourceContainer(BlobStorageMigrationSource source)
        {
            var account = GetSourceAccount(source);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(source.ContainerName);
            return container;
        }

        private class MigrateToLatestComparisonEnumerable : BlobComparisonEnumerable
        {
            private readonly Func<string, string> _normalizedName;

            public MigrateToLatestComparisonEnumerable(
                IAsyncEnumerable<BlobContext> left,
                IAsyncEnumerable<BlobContext> right,
                Func<string, string> normalizedName) : base(left, right)
            {
                _normalizedName = normalizedName;
            }

            protected override IComparableBlob GetLeftComparableBlobOrNull(BlobContext left)
            {
                return NormalizeNameBlob.CreateOrNull(left, _normalizedName);
            }

            protected override IComparableBlob GetRightComparableBlobOrNull(BlobContext right)
            {
                return NormalizeNameBlob.CreateOrNull(right, _normalizedName);
            }
        }

        private class NormalizeNameBlob : IComparableBlob
        {
            private readonly ICloudBlob _blob;
            private readonly Func<string, string> _normalizedName;

            private NormalizeNameBlob(ICloudBlob blob, Func<string, string> normalizedName)
            {
                _blob = blob ?? throw new ArgumentNullException(nameof(blob));
                _normalizedName = normalizedName ?? throw new ArgumentNullException(nameof(normalizedName));
            }

            public string Name => _normalizedName(_blob.Name);
            public Type BlobType => _blob.GetType();
            public long Length => _blob.Properties.Length;
            public string ContentMD5 => _blob.Properties.ContentMD5;

            public static NormalizeNameBlob CreateOrNull(
                BlobContext blobContext,
                Func<string, string> normalizedName)
            {
                if (blobContext == null)
                {
                    return null;
                }

                return new NormalizeNameBlob(blobContext.Blob, normalizedName);
            }
        }
    }
}
