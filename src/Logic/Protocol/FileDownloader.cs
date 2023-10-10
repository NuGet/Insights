// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;

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
        private readonly HttpSource _httpSource;
        private readonly TempStreamService _tempStreamService;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<FileDownloader> _logger;

        public FileDownloader(
            HttpSource httpSource,
            TempStreamService tempStreamService,
            HttpZipProvider httpZipProvider,
            ITelemetryClient telemetryClient,
            ILogger<FileDownloader> logger)
        {
            _httpSource = httpSource;
            _tempStreamService = tempStreamService;
            _httpZipProvider = httpZipProvider;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task<(ILookup<string, string> Headers, TempStreamResult Body)?> DownloadUrlToFileAsync(string url, CancellationToken token)
        {
            return await DownloadUrlToFileAsync(url, allowNotFound: true, token);
        }

        private async Task<(ILookup<string, string> Headers, TempStreamResult Body)?> DownloadUrlToFileAsync(string url, bool allowNotFound, CancellationToken token)
        {
            var nuGetLogger = _logger.ToNuGetLogger();
            var writer = _tempStreamService.GetWriter();

            ILookup<string, string>? headers = null;
            TempStreamResult? result = null;
            try
            {
                do
                {
                    result = await _httpSource.ProcessResponseWithRetryAsync(
                        new HttpSourceRequest(url, nuGetLogger),
                        async response =>
                        {
                            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                            {
                                return null;
                            }

                            response.EnsureSuccessStatusCode();

                            headers = Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()
                                .Concat(response.Headers)
                                .Concat(response.Content.Headers)
                                .SelectMany(x => x.Value.Select(y => new { x.Key, Value = y }))
                                .ToLookup(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

                            if (response.Content.Headers.ContentLength is null)
                            {
                                throw new InvalidOperationException($"No Content-Length header was returned for package URL: {url}");
                            }

                            using var networkStream = await response.Content.ReadAsStreamAsync();
                            return await writer.CopyToTempStreamAsync(
                                networkStream,
                                response.Content.Headers.ContentLength.Value,
                                IncrementalHash.CreateAll());
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
                result?.Dispose();
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
                var result = await DownloadUrlToFileAsync(url, allowNotFound: false, CancellationToken.None);
                try
                {
                    using var fullReader = new ZipDirectoryReader(result.Value.Body.Stream, leaveOpen: false, result.Value.Headers);
                    return await fullReader.ReadUncompressedFileDataAsync(zipDirectory, signatureEntry);
                }
                finally
                {
                    if (result.HasValue)
                    {
                        result.Value.Body.Dispose();
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
                        var reader = await _httpZipProvider.GetReaderAsync(new Uri(url));

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
                    var result = await DownloadUrlToFileAsync(url, CancellationToken.None);

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
