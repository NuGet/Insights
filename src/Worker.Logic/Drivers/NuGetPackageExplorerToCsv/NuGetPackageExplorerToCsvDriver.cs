// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml;
using NuGet.Packaging.Core;
using NuGetPe;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public class NuGetPackageExplorerToCsvDriver :
        ICatalogLeafToCsvDriver<NuGetPackageExplorerRecord, NuGetPackageExplorerFile>,
        ICsvResultStorage<NuGetPackageExplorerRecord>,
        ICsvResultStorage<NuGetPackageExplorerFile>
    {
        public const int FileBufferSize = 4 * 1024 * 1024;

        private readonly CatalogClient _catalogClient;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly Func<HttpClient> _httpClientFactory;
        private readonly TemporaryFileProvider _temporaryFileProvider;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<NuGetPackageExplorerToCsvDriver> _logger;

        public NuGetPackageExplorerToCsvDriver(
            CatalogClient catalogClient,
            FlatContainerClient flatContainerClient,
            Func<HttpClient> httpClientFactory,
            TemporaryFileProvider temporaryFileProvider,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<NuGetPackageExplorerToCsvDriver> logger)
        {
            _catalogClient = catalogClient;
            _flatContainerClient = flatContainerClient;
            _httpClientFactory = httpClientFactory;
            _temporaryFileProvider = temporaryFileProvider;
            _options = options;
            _logger = logger;
        }

        public bool SingleMessagePerId => false;
        string ICsvResultStorage<NuGetPackageExplorerRecord>.ResultContainerName => _options.Value.NuGetPackageExplorerContainerName;
        string ICsvResultStorage<NuGetPackageExplorerFile>.ResultContainerName => _options.Value.NuGetPackageExplorerFileContainerName;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSets<NuGetPackageExplorerRecord, NuGetPackageExplorerFile>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            (var record, var files) = await ProcessLeafInternalAsync(leafScan);
            return DriverResult.Success(new CsvRecordSets<NuGetPackageExplorerRecord, NuGetPackageExplorerFile>(
                record != null ? [record] : [],
                files ?? []));
        }

        private async Task<(NuGetPackageExplorerRecord, IReadOnlyList<NuGetPackageExplorerFile>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return (
                    new NuGetPackageExplorerRecord(scanId, scanTimestamp, leaf),
                    [new NuGetPackageExplorerFile(scanId, scanTimestamp, leaf)]
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var tempDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "npe"));
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var tempFileName = TempStreamWriter.GetTempFileNameFactory(
                    leafScan.PackageId,
                    leafScan.PackageVersion,
                    "npe",
                    ".nupkg")();
                var tempPath = Path.Combine(tempDir, tempFileName);
                try
                {
                    var exists = await DownloadToFileAsync(leaf, leafScan.AttemptCount, tempPath);
                    if (!exists)
                    {
                        // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted record.
                        return (null, null);
                    }

                    _logger.LogInformation(
                        "Loading ZIP package for {Id} {Version} on attempt {AttemptCount}.",
                        leaf.PackageId,
                        leaf.PackageVersion,
                        leafScan.AttemptCount);

                    ZipPackage zipPackage;
                    try
                    {
                        zipPackage = new ZipPackage(tempPath);
                    }
                    catch (Exception ex) when (ex is InvalidDataException
                                            || ex is ArgumentException
                                            || ex is PackagingException
                                            || ex is XmlException
                                            || ex is InvalidOperationException
                                            || ex.Message.Contains("Enabling license acceptance requires a license or a licenseUrl to be specified.", StringComparison.Ordinal)
                                            || ex.Message.Contains("Authors is required.", StringComparison.Ordinal)
                                            || ex.Message.Contains("Description is required.", StringComparison.Ordinal)
                                            || ex.Message.Contains("Url cannot be empty.", StringComparison.Ordinal)
                                            || (ex.Message.Contains("Assembly reference ", StringComparison.Ordinal) && ex.Message.Contains(" contains invalid characters.", StringComparison.Ordinal)))
                    {
                        _logger.LogWarning(ex, "Package {Id} {Version} had invalid metadata.", leaf.PackageId, leaf.PackageVersion);
                        return MakeSingleItem(scanId, scanTimestamp, leaf, NuGetPackageExplorerResultType.InvalidMetadata);
                    }

                    using (zipPackage)
                    {
                        var symbolValidator = new SymbolValidator(zipPackage, zipPackage.Source, rootFolder: null, _httpClientFactory(), _temporaryFileProvider);

                        SymbolValidatorResult symbolValidatorResult;
                        using (var cts = new CancellationTokenSource())
                        {
                            var delayTask = Task.Delay(TimeSpan.FromMinutes(4), cts.Token);
                            _logger.LogInformation(
                                "Starting symbol validation for {Id} {Version} on attempt {AttemptCount}.",
                                leaf.PackageId,
                                leaf.PackageVersion,
                                leafScan.AttemptCount);
                            var symbolValidatorTask = symbolValidator.Validate(cts.Token);

                            var resultTask = await Task.WhenAny(delayTask, symbolValidatorTask);
                            if (resultTask == delayTask)
                            {
                                cts.Cancel();

                                if (leafScan.AttemptCount > 3)
                                {
                                    _logger.LogWarning("Package {Id} {Version} had its symbol validation timeout.", leaf.PackageId, leaf.PackageVersion);
                                    return MakeSingleItem(scanId, scanTimestamp, leaf, NuGetPackageExplorerResultType.Timeout);
                                }
                                else
                                {
                                    throw new TimeoutException("The NuGetPackageExplorer symbol validator task timed out.");
                                }
                            }
                            else
                            {
                                symbolValidatorResult = await symbolValidatorTask;
                                cts.Cancel();
                            }
                        }

                        _logger.LogInformation(
                            "Loading signature data for {Id} {Version} on attempt {AttemptCount}.",
                            leaf.PackageId,
                            leaf.PackageVersion,
                            leafScan.AttemptCount);

                        await zipPackage.LoadSignatureDataAsync();

                        using var fileStream = zipPackage.GetStream();
                        var record = new NuGetPackageExplorerRecord(scanId, scanTimestamp, leaf)
                        {
                            SourceLinkResult = symbolValidatorResult.SourceLinkResult,
                            DeterministicResult = symbolValidatorResult.DeterministicResult,
                            CompilerFlagsResult = symbolValidatorResult.CompilerFlagsResult,
                            IsSignedByAuthor = zipPackage.PublisherSignature != null,
                        };

                        var files = new List<NuGetPackageExplorerFile>();

                        try
                        {
                            _logger.LogInformation(
                                "Getting all files for {Id} {Version} on attempt {AttemptCount}.",
                                leaf.PackageId,
                                leaf.PackageVersion,
                                leafScan.AttemptCount);

                            foreach (var file in symbolValidator.GetAllFiles())
                            {
                                var compilerFlags = file.DebugData?.CompilerFlags.ToDictionary(k => k.Key, v => v.Value);

                                var sourceUrls = file.DebugData?.Sources.Where(x => x.Url != null).Select(x => x.Url);
                                var sourceUrlRepoInfo = sourceUrls != null ? SourceUrlRepoParser.GetSourceRepoInfo(sourceUrls) : null;

                                files.Add(new NuGetPackageExplorerFile(scanId, scanTimestamp, leaf)
                                {
                                    Path = file.Path,
                                    Extension = file.Extension,
                                    HasCompilerFlags = file.DebugData?.HasCompilerFlags,
                                    HasSourceLink = file.DebugData?.HasSourceLink,
                                    HasDebugInfo = file.DebugData?.HasDebugInfo,
                                    PdbType = file.DebugData?.PdbType,
                                    CompilerFlags = KustoDynamicSerializer.Serialize(compilerFlags),
                                    SourceUrlRepoInfo = KustoDynamicSerializer.Serialize(sourceUrlRepoInfo),
                                });
                            }
                        }
                        catch (Exception ex) when (ex is FileNotFoundException || ex is FormatException)
                        {
                            // handles https://github.com/NuGetPackageExplorer/NuGetPackageExplorer/issues/1505
                            _logger.LogWarning(ex, "Could not get symbol validator files for {Id} {Version}.", leaf.PackageId, leaf.PackageVersion);
                            return MakeSingleItem(scanId, scanTimestamp, leaf, NuGetPackageExplorerResultType.InvalidMetadata);
                        }

                        if (files.Count == 0)
                        {
                            record.ResultType = NuGetPackageExplorerResultType.NothingToValidate;

                            // Add a marker "nothing to validate" record to the files table so that all tables have the
                            // same set of identities.
                            files.Add(new NuGetPackageExplorerFile(scanId, scanTimestamp, leaf)
                            {
                                ResultType = NuGetPackageExplorerResultType.NothingToValidate,
                            });
                        }

                        return (record, files);
                    }
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try
                        {
                            File.Delete(tempPath);
                        }
                        catch (Exception ex)
                        {
                            // Best effort.
                            _logger.LogError(ex, "Could not delete {TempPath} during NuGet Package Explorer clean up.", tempPath);
                        }
                    }
                }
            }
        }

        private async Task<bool> DownloadToFileAsync(PackageDetailsCatalogLeaf leaf, int attemptCount, string path)
        {
            var contentUrl = await _flatContainerClient.GetPackageContentUrlAsync(leaf.PackageId, leaf.PackageVersion);

            _logger.LogInformation(
                "Downloading .nupkg for {Id} {Version} on attempt {AttemptCount}.",
                leaf.PackageId,
                leaf.PackageVersion,
                attemptCount);

            var httpClient = _httpClientFactory();
            return await httpClient.ProcessResponseWithRetriesAsync(
                () => new HttpRequestMessage(HttpMethod.Get, contentUrl),
                async response =>
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return false;
                    }

                    response.EnsureSuccessStatusCode();

                    var length = response.Content.Headers.ContentLength.Value;

                    using (var source = await response.Content.ReadAsStreamAsync())
                    using (var hasher = IncrementalHash.CreateNone())
                    using (var destination = new FileStream(
                        path,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.Read,
                        bufferSize: 4096,
                        FileOptions.Asynchronous))
                    {
                        await destination.SetLengthAndWriteAsync(length);
                        await source.CopyToSlowAsync(
                            destination,
                            length,
                            bufferSize: FileBufferSize,
                            hasher: hasher,
                            logger: _logger);
                    }

                    return true;
                },
                _logger,
                CancellationToken.None);
        }

        private static (NuGetPackageExplorerRecord, NuGetPackageExplorerFile[]) MakeSingleItem(
            Guid scanId,
            DateTimeOffset scanTimestamp,
            PackageDetailsCatalogLeaf leaf,
            NuGetPackageExplorerResultType type)
        {
            return (
                new NuGetPackageExplorerRecord(scanId, scanTimestamp, leaf) { ResultType = type },
                new[] { new NuGetPackageExplorerFile(scanId, scanTimestamp, leaf) { ResultType = type } }
            );
        }
    }
}
