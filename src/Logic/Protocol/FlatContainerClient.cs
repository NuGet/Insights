// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class FlatContainerClient
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FileDownloader _fileDownloader;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public FlatContainerClient(
            ServiceIndexCache serviceIndexCache,
            FileDownloader fileDownloader,
            IOptions<NuGetInsightsSettings> options)
        {
            _serviceIndexCache = serviceIndexCache;
            _fileDownloader = fileDownloader;
            _options = options;
        }

        public async Task<string> GetPackageContentUrlAsync(string id, string version)
        {
            var baseUrl = await GetBaseUrlAsync();
            return GetPackageContentUrl(baseUrl, id, version);
        }

        private string GetPackageContentUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var url = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg";
            return url;
        }

        public async Task<string> GetPackageManifestUrlAsync(string id, string version)
        {
            var baseUrl = await GetBaseUrlAsync();
            return GetPackageManifestUrl(baseUrl, id, version);
        }

        private string GetPackageManifestUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var url = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.nuspec";
            return url;
        }

        public async Task<string> GetPackageReadmeUrlAsync(string id, string version)
        {
            var baseUrl = await GetBaseUrlAsync();
            return GetPackageReadmeUrl(baseUrl, id, version);
        }

        private string GetPackageReadmeUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var url = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/readme";
            return url;
        }

        public async Task<(string? ContentType, TempStreamResult Body)?> DownloadPackageIconToFileAsync(string id, string version, CancellationToken token)
        {
            var url = await GetPackageIconUrlAsync(id, version);
            var result = await _fileDownloader.DownloadUrlToFileAsync(
                url,
                TempStreamWriter.GetTempFileNameFactory(
                    id,
                    version,
                    "icon",
                    ".tmp"),
                IncrementalHash.CreateSHA256,
                requireContentLength: false,
                token);
            if (result is null)
            {
                return null;
            }

            return (result.Value.Headers["Content-Type"].FirstOrDefault(), result.Value.Body);
        }

        private async Task<string> GetPackageIconUrlAsync(string id, string version)
        {
            var baseUrl = await GetBaseUrlAsync();
            return GetPackageIconUrl(baseUrl, id, version);
        }

        private string GetPackageIconUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var url = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/icon";
            return url;
        }

        private async Task<string> GetPackageLicenseUrlAsync(string id, string version)
        {
            var baseUrl = await GetBaseUrlAsync();
            return GetPackageLicenseUrl(baseUrl, id, version);
        }

        private string GetPackageLicenseUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var url = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/license";
            return url;
        }

        public async Task<TempStreamResult?> DownloadPackageLicenseToFileAsync(string id, string version, CancellationToken token)
        {
            var url = await GetPackageLicenseUrlAsync(id, version);
            var result = await _fileDownloader.DownloadUrlToFileAsync(
                url,
                TempStreamWriter.GetTempFileNameFactory(
                    id,
                    version,
                    "license",
                    ".txt"),
                IncrementalHash.CreateSHA256,
                token);
            if (result is null)
            {
                return null;
            }

            return result.Value.Body;
        }

        private async Task<string> GetBaseUrlAsync()
        {
            if (_options.Value.FlatContainerBaseUrlOverride != null)
            {
                return _options.Value.FlatContainerBaseUrlOverride;
            }

            return await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
        }
    }
}
