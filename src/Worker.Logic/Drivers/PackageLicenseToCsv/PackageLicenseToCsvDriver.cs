// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;

namespace NuGet.Insights.Worker.PackageLicenseToCsv
{
    public class PackageLicenseToCsvDriver : ICatalogLeafToCsvDriver<PackageLicense>, ICsvResultStorage<PackageLicense>
    {
        private readonly CatalogClient _catalogClient;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageLicenseToCsvDriver(
            CatalogClient catalogClient,
            FlatContainerClient flatContainerClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _flatContainerClient = flatContainerClient;
            _options = options;
        }

        public string ResultContainerName => _options.Value.PackageLicenseContainerName;
        public bool SingleMessagePerId => false;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<IReadOnlyList<PackageLicense>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            (var resultType, var records) = await ProcessLeafInternalAsync(leafScan);
            if (resultType == TempStreamResultType.SemaphoreNotAvailable)
            {
                return DriverResult.TryAgainLater<IReadOnlyList<PackageLicense>>();
            }

            return DriverResult.Success(records);
        }

        private async Task<(TempStreamResultType, IReadOnlyList<PackageLicense>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return (
                    TempStreamResultType.Success,
                    new List<PackageLicense> { new PackageLicense(scanId, scanTimestamp, leaf) }
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                PackageLicenseResultType resultType;
                if (!string.IsNullOrWhiteSpace(leaf.LicenseFile))
                {
                    resultType = PackageLicenseResultType.File;

                }
                else if (!string.IsNullOrEmpty(leaf.LicenseExpression))
                {
                    resultType = PackageLicenseResultType.Expression;
                }
                else if (!string.IsNullOrEmpty(leaf.LicenseUrl))
                {
                    resultType = PackageLicenseResultType.Url;
                }
                else
                {
                    resultType = PackageLicenseResultType.None;
                }

                var record = new PackageLicense(scanId, scanTimestamp, leaf)
                {
                    ResultType = resultType,
                    Url = leaf.LicenseUrl,
                    Expression = leaf.LicenseExpression,
                    File = leaf.LicenseFile,
                };

                if (!string.IsNullOrWhiteSpace(leaf.LicenseExpression))
                {
                    try
                    {
                        var parsedExpression = NuGetLicenseExpression.Parse(leaf.LicenseExpression);
                        var licenses = new HashSet<string>();
                        var nonStandardLicenses = new HashSet<string>();
                        var exceptions = new HashSet<string>();
                        parsedExpression.OnEachLeafNode(
                            license =>
                            {
                                licenses.Add(license.Identifier);
                                if (!license.IsStandardLicense)
                                {
                                    nonStandardLicenses.Add(license.Identifier);
                                }
                            },
                            exception => exceptions.Add(exception.Identifier));

                        record.ExpressionParsed = KustoDynamicSerializer.Serialize(parsedExpression);
                        record.ExpressionLicenses = KustoDynamicSerializer.Serialize(licenses.OrderBy(x => x, StringComparer.Ordinal).ToList());
                        record.ExpressionExceptions = KustoDynamicSerializer.Serialize(exceptions.OrderBy(x => x, StringComparer.Ordinal).ToList());
                        record.ExpressionNonStandardLicenses = KustoDynamicSerializer.Serialize(nonStandardLicenses.OrderBy(x => x, StringComparer.Ordinal).ToList());
                        record.GeneratedUrl = new LicenseMetadata(
                            LicenseType.Expression,
                            leaf.LicenseExpression,
                            parsedExpression,
                            Array.Empty<string>(),
                            LicenseMetadata.CurrentVersion).LicenseUrl.AbsoluteUri;
                        record.ExpressionHasDeprecatedIdentifier = false;
                    }
                    catch (NuGetLicenseExpressionParsingException ex) when (Regex.IsMatch(ex.Message, "^The identifier '.+' is deprecated.$"))
                    {
                        record.ExpressionHasDeprecatedIdentifier = true;
                    }
                }

                if (!string.IsNullOrEmpty(leaf.LicenseFile))
                {
                    record.GeneratedUrl = new LicenseMetadata(
                        LicenseType.File,
                        license: leaf.LicenseFile,
                        expression: null,
                        Array.Empty<string>(),
                        LicenseMetadata.CurrentVersion).LicenseUrl.AbsoluteUri;
                }

                var result = await _flatContainerClient.DownloadPackageLicenseToFileAsync(
                    leafScan.PackageId,
                    leafScan.PackageVersion,
                    CancellationToken.None);

                if (result != null)
                {
                    await using (result)
                    {
                        if (result.Type == TempStreamResultType.SemaphoreNotAvailable)
                        {
                            return (
                                TempStreamResultType.SemaphoreNotAvailable,
                                null
                            );
                        }

                        record.FileLength = result.Stream.Length;
                        record.FileSHA256 = result.Hash.SHA256.ToBase64();

                        using (var reader = new StreamReader(result.Stream))
                        {
                            record.FileContent = reader.ReadToEnd();
                        }
                    }
                }

                return (
                    TempStreamResultType.Success,
                    new List<PackageLicense> { record }
                );
            }
        }
    }
}
