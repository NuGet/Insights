// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Knapcode.MiniZip;
using Microsoft.AspNetCore.WebUtilities;

#nullable enable

namespace NuGet.Insights
{
    public enum ArtifactFileType
    {
        Nupkg,
        Snupkg,
    }

    public class FileDownloader
    {
        private readonly Func<HttpClient> _httpClientFactory;
        private readonly Func<HttpClient> _noDecompressionHttpClientFactory;
        private readonly TempStreamService _tempStreamService;
        private readonly Func<HttpZipProvider> _httpZipProviderFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<FileDownloader> _logger;

        public FileDownloader(
            Func<HttpClient> httpClientFactory,
            Func<HttpClient> noDecompressionHttpClientFactory,
            TempStreamService tempStreamService,
            Func<HttpZipProvider> httpZipProviderFactory,
            ITelemetryClient telemetryClient,
            ILogger<FileDownloader> logger)
        {
            _httpClientFactory = httpClientFactory;
            _noDecompressionHttpClientFactory = noDecompressionHttpClientFactory;
            _tempStreamService = tempStreamService;
            _httpZipProviderFactory = httpZipProviderFactory;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task<(ILookup<string, string> Headers, TempStreamResult Body)?> DownloadUrlToFileAsync(
            string url,
            Func<string> getTempFileName,
            Func<IIncrementalHash> getHasher,
            CancellationToken token)
        {
            return await DownloadUrlToFileAsync(url, getTempFileName, getHasher, allowNotFound: true, requireContentLength: true, token);
        }

        public async Task<(ILookup<string, string> Headers, TempStreamResult Body)?> DownloadUrlToFileAsync(
            string url,
            Func<string> getTempFileName,
            Func<IIncrementalHash> getHasher,
            bool requireContentLength,
            CancellationToken token)
        {
            return await DownloadUrlToFileAsync(url, getTempFileName, getHasher, allowNotFound: true, requireContentLength, token);
        }

        private async Task<(ILookup<string, string> Headers, TempStreamResult Body)?> DownloadUrlToFileAsync(
            string url,
            Func<string> getTempFileName,
            Func<IIncrementalHash> getHasher,
            bool allowNotFound,
            bool requireContentLength,
            CancellationToken token)
        {
            var writer = _tempStreamService.GetWriter();

            return await DownloadUrlToFileAsync(
                url,
                allowNotFound,
                requireContentLength,
                async (networkStream, contentLength) =>
                {
                    return await writer.CopyToTempStreamAsync(
                        networkStream,
                        getTempFileName,
                        contentLength,
                        getHasher());
                },
                token);
        }

        public async Task<(ILookup<string, string> Headers, TempStreamResult Body)?> DownloadUrlToFileAsync(
            string url,
            bool allowNotFound,
            bool requireContentLength,
            Func<Stream, long, Task<TempStreamResult>> getTempStream,
            CancellationToken token)
        {
            ILookup<string, string>? headers = null;
            TempStreamResult? result = null;
            bool useAcceptEncoding = true;
            try
            {
                do
                {
                    var httpClient = useAcceptEncoding ? _httpClientFactory() : _noDecompressionHttpClientFactory();
                    result = await httpClient.ProcessResponseWithRetriesAsync(
                        () => new HttpRequestMessage(HttpMethod.Get, url),
                        async response =>
                        {
                            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                            {
                                return null;
                            }

                            response.EnsureSuccessStatusCode();

                            headers = response.GetHeaderLookup();

                            long contentLength;
                            if (response.Content.Headers.ContentLength is null)
                            {
                                _logger.LogTransientWarning(
                                    "No Content-Length header was returned for URL: {Url}. Use Accept-Encoding: {UseAcceptEncoding}. Request headers: {RequestHeaders}Response headers: {ResponseHeaders}",
                                    url,
                                    useAcceptEncoding,
                                    response.RequestMessage?.DebugHeaders(prefixNewLine: true) ?? $"(no request message){Environment.NewLine}",
                                    response.DebugHeaders(prefixNewLine: true));

                                if (requireContentLength)
                                {
                                    if (useAcceptEncoding)
                                    {
                                        useAcceptEncoding = false;
                                        return TempStreamResult.NeedNewStream();
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException($"No Content-Length header was returned for URL: {url}");
                                    }
                                }
                                else
                                {
                                    contentLength = -1;
                                }
                            }
                            else
                            {
                                contentLength = response.Content.Headers.ContentLength.Value;
                            }

                            using var networkStream = await response.Content.ReadAsStreamAsync();
                            return await getTempStream(networkStream, contentLength);
                        },
                        _logger,
                        token);

                    if (result == null)
                    {
                        return null;
                    }
                }
                while (result.Type == TempStreamResultType.NeedNewStream);

                return (headers!, result);
            }
            catch
            {
                if (result is not null)
                {
                    await result.DisposeAsync();
                }
                throw;
            }
        }

        public async Task<ZipDirectoryReader?> GetZipDirectoryReaderAsync(string id, string version, ArtifactFileType fileType, string url)
        {
            return await GetZipDirectoryReaderAsync(id, version, fileType, url, ZipDownloadMode.DefaultMiniZip);
        }

        public async Task<byte[]> GetSignatureBytesAsync(ZipDirectoryReader reader, ZipDirectory zipDirectory, string id, string version, string url)
        {
            var signatureEntry = zipDirectory.Entries.Single(x => x.GetName() == ".signature.p7s");

            try
            {
                return await reader.ReadUncompressedFileDataAsync(zipDirectory, signatureEntry);
            }
            catch (MiniZipHttpException ex)
            {
                _logger.LogTransientWarning(ex, "Fetching signature bytes from {Url} for {Id} {Version} failed using MiniZip. Trying again with a full download.", url, id, version);
                var result = await DownloadUrlToFileAsync(
                    url,
                    () => $"{StorageUtility.GenerateDescendingId()}.{id}.{version}.sig-bytes.nupkg",
                    IncrementalHash.CreateNone,
                    allowNotFound: false,
                    requireContentLength: true,
                    CancellationToken.None);
                try
                {
                    using var fullReader = new ZipDirectoryReader(result.Value.Body.Stream, leaveOpen: false, result.Value.Headers);
                    return await fullReader.ReadUncompressedFileDataAsync(zipDirectory, signatureEntry);
                }
                finally
                {
                    if (result.HasValue)
                    {
                        await result.Value.Body.DisposeAsync();
                    }
                }
            }
        }

        private enum ZipDownloadMode
        {
            DefaultMiniZip,
            CacheBustMiniZip,
            FullDownload,
        }

        public static string GetFileExtension(ArtifactFileType fileType)
        {
            return fileType switch
            {
                ArtifactFileType.Nupkg => ".nupkg",
                ArtifactFileType.Snupkg => ".snupkg",
                _ => throw new NotImplementedException(),
            };
        }

        private async Task<ZipDirectoryReader?> GetZipDirectoryReaderAsync(string id, string version, ArtifactFileType fileType, string url, ZipDownloadMode mode)
        {
            var metric = _telemetryClient.GetMetric(
                $"{nameof(FileDownloader)}.{nameof(GetZipDirectoryReaderAsync)}.DurationMs",
                "ArtifactFileType",
                "DownloadMode");

            var sw = Stopwatch.StartNew();

            var notFoundMetric = _telemetryClient.GetMetric(
                $"{nameof(FileDownloader)}.{nameof(GetZipDirectoryReaderAsync)}.NotFound",
                "PackageId",
                "PackageVersion",
                "ArtifactFileType",
                "DownloadMode");

            try
            {

                if (mode == ZipDownloadMode.DefaultMiniZip || mode == ZipDownloadMode.CacheBustMiniZip)
                {
                    // I've noticed cases where NuGet.org CDN caches a request with a specific If-* condition header in the
                    // request. When subsequent requests come with a different If-* condition header, Blob Storage errors out
                    // with an HTTP 400 and a "MultipleConditionHeadersNotSupported" error code. This seems like a bug in the
                    // NuGet CDN, where a "Vary: If-Match" or similar is missing.
                    if (mode == ZipDownloadMode.CacheBustMiniZip)
                    {
                        url = QueryHelpers.AddQueryString(url, "cache-bust", Guid.NewGuid().ToString());
                    }

                    try
                    {
                        var httpZipProvider = _httpZipProviderFactory();
                        var reader = await httpZipProvider.GetReaderAsync(new Uri(url));

                        // Read the ZIP reader proactively to warm the cache to find any HTTP exceptions that might occur.
                        await reader.ReadAsync();

                        return reader;
                    }
                    catch (MiniZipHttpException notFoundEx) when (notFoundEx.StatusCode == HttpStatusCode.NotFound)
                    {
                        notFoundMetric.TrackValue(1, id, version, fileType.ToString(), mode.ToString());
                        return null;
                    }
                    catch (MiniZipHttpException defaultEx) when (mode == ZipDownloadMode.DefaultMiniZip)
                    {
                        _logger.LogTransientWarning(defaultEx, "Fetching {FileType} {Url} for {Id} {Version} failed using MiniZip. Trying again with cache busting.", fileType, url, id, version);
                        return await GetZipDirectoryReaderAsync(id, version, fileType, url, ZipDownloadMode.CacheBustMiniZip);
                    }
                    catch (MiniZipHttpException cacheBustEx) when (mode == ZipDownloadMode.CacheBustMiniZip)
                    {
                        _logger.LogTransientWarning(cacheBustEx, "Fetching {FileType} {Url} for {Id} {Version} failed using MiniZip. Trying again with a full download.", fileType, url, id, version);
                        return await GetZipDirectoryReaderAsync(id, version, fileType, url, ZipDownloadMode.FullDownload);
                    }
                }
                else if (mode == ZipDownloadMode.FullDownload)
                {
                    var result = await DownloadUrlToFileAsync(
                        url,
                        () => $"{StorageUtility.GenerateDescendingId()}.{id}.{version}.reader{GetFileExtension(fileType)}",
                        IncrementalHash.CreateNone,
                        CancellationToken.None);

                    if (result is null)
                    {
                        notFoundMetric.TrackValue(1, id, version, fileType.ToString(), mode.ToString());
                        return null;
                    }

                    return new ZipDirectoryReader(result.Value.Body.Stream, leaveOpen: false, result.Value.Headers);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            finally
            {
                metric.TrackValue(sw.ElapsedMilliseconds, fileType.ToString(), mode.ToString());
            }
        }
    }
}
