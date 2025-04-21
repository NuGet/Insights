// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;

#nullable enable

namespace NuGet.Insights.FileSystemHttpCache
{
    public partial class FileSystemHttpCacheHandler : DelegatingHandler
    {
        private static readonly ConcurrentDictionary<(string Directory, string CacheKey), SemaphoreSlim> CacheKeyToLock = new();

        public const string CacheHeaderName = $"X-{nameof(FileSystemHttpCacheHandler)}";
        public const string CacheHeaderValue = "true";

        private static readonly FrozenSet<string> SensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
        }.ToFrozenSet();

        private readonly IReadOnlyList<FileSystemHttpCacheSettings> _cacheSettings;
        private readonly FileSystemHttpCacheMode _mode;

        public FileSystemHttpCacheHandler(
            IReadOnlyList<FileSystemHttpCacheSettings> cacheSettings,
            FileSystemHttpCacheMode mode)
        {
            if (mode == FileSystemHttpCacheMode.Disabled)
            {
                throw new ArgumentException($"File system cache handler mode cannot be {mode}.");
            }

            _cacheSettings = cacheSettings;
            _mode = mode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            FileSystemHttpCacheSettings? cacheSettings = null;
            foreach (var candidate in _cacheSettings)
            {
                if (candidate.IsApplicable(request))
                {
                    cacheSettings = candidate;
                    break;
                }
            }

            if (cacheSettings is null)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var cacheKey = await GetCacheKeyAsync(request, cacheSettings);
            if (cacheKey is null)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var cacheKeyLock = CacheKeyToLock.GetOrAdd((cacheKey.Directory, cacheKey.Key), _ => new SemaphoreSlim(initialCount: 1));
            try
            {
                var acquired = await cacheKeyLock.WaitAsync(TimeSpan.FromSeconds(120));
                if (!acquired)
                {
                    cacheKeyLock = null;
                    throw new TimeoutException($"Failed to acquire lock for cache key: {cacheKey.Key}.");
                }

                return _mode switch
                {
                    FileSystemHttpCacheMode.ReadAndWriteOnlyMissing => await ReadAndWriteOnlyMissingAsync(cacheSettings, cacheKey, request, cancellationToken),
                    FileSystemHttpCacheMode.WriteOnlyMissing => await WriteOnlyMissingAsync(cacheSettings, cacheKey, request, cancellationToken),
                    FileSystemHttpCacheMode.WriteAlways => await WriteAlwaysAsync(cacheSettings, cacheKey, request, cancellationToken),
                    FileSystemHttpCacheMode.ReadOnly => await ReadOnlyAsync(cacheSettings, cacheKey, request, cancellationToken),
                    _ => throw new NotImplementedException($"Unknown cache mode: {_mode}."),
                };
            }
            finally
            {
                cacheKeyLock?.Release();
            }
        }

        private async Task<HttpResponseMessage> ReadAndWriteOnlyMissingAsync(FileSystemHttpCacheSettings cacheSettings, FileSystemHttpCacheKey cacheKey, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (HasCachedEntry(cacheKey))
            {
                var cachedResponse = await ReadResponseFromCacheAsync(request, cacheKey, disposeReplacedRequest: true);
                if (cachedResponse.HasValue)
                {
                    if (cachedResponse.Value.Response.StatusCode < HttpStatusCode.BadRequest)
                    {
                        await LoadResponseBodyFromCacheAsync(cacheKey, cachedResponse.Value.Response, cachedResponse.Value.ContentHeaders);
                        return cachedResponse.Value.Response;
                    }
                    else
                    {
                        cachedResponse.Value.Response.Dispose();
                    }
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            await CacheRequestAsync(cacheSettings, cacheKey, request);
            await CacheResponseAsync(cacheSettings, cacheKey, response);

            return response;
        }

        private async Task<HttpResponseMessage> WriteOnlyMissingAsync(FileSystemHttpCacheSettings cacheSettings, FileSystemHttpCacheKey cacheKey, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var shouldWriteCache = true;
            if (HasCachedEntry(cacheKey))
            {
                var cachedResponse = await ReadResponseFromCacheAsync(request, cacheKey, disposeReplacedRequest: false);
                if (cachedResponse.HasValue)
                {
                    cachedResponse.Value.Response.Dispose();
                    shouldWriteCache = cachedResponse.Value.Response.StatusCode >= HttpStatusCode.BadRequest;
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (shouldWriteCache)
            {
                await CacheRequestAsync(cacheSettings, cacheKey, request);
                await CacheResponseAsync(cacheSettings, cacheKey, response);
            }

            return response;
        }

        private async Task<HttpResponseMessage> WriteAlwaysAsync(FileSystemHttpCacheSettings cacheSettings, FileSystemHttpCacheKey cacheKey, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            await CacheRequestAsync(cacheSettings, cacheKey, request);
            await CacheResponseAsync(cacheSettings, cacheKey, response);

            return response;
        }

        private async Task<HttpResponseMessage> ReadOnlyAsync(FileSystemHttpCacheSettings cacheSettings, FileSystemHttpCacheKey cacheKey, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (HasCachedEntry(cacheKey))
            {
                var cachedResponse = await ReadResponseFromCacheAsync(request, cacheKey, disposeReplacedRequest: true);
                if (cachedResponse.HasValue)
                {
                    await LoadResponseBodyFromCacheAsync(cacheKey, cachedResponse.Value.Response, cachedResponse.Value.ContentHeaders);
                    return cachedResponse.Value.Response;
                }
            }

            throw new InvalidOperationException(
                $"Cannot send request to the network when {_mode} {nameof(FileSystemHttpCacheMode)} is set. " +
                $"Missed cache key: '{cacheKey.Key}'. " +
                $"Check the other files with similar cache keys in the {cacheKey.Directory} directory if you expect the request to already be cached. " +
                $"Or, switch the mode to {nameof(FileSystemHttpCacheMode.ReadAndWriteOnlyMissing)} to populate the file system cache.{Environment.NewLine}" +
                $"Cache key info: {cacheKey.KeyInfo}");
        }

        private static async Task CacheRequestAsync(FileSystemHttpCacheSettings cacheSettings, FileSystemHttpCacheKey cacheKey, HttpRequestMessage request)
        {
            if (cacheKey.KeyInfo is not null)
            {
                await WriteAndSwapAsync(cacheSettings, cacheKey, FileSystemHttpCacheEntryType.KeyInfo, async (fileStream) =>
                {
                    using var writer = new StreamWriter(fileStream, leaveOpen: true);
                    await writer.WriteAsync(cacheKey.KeyInfo);
                }, request, response: null);
            }

            if (request.Content is not null)
            {
                await WriteContentAsync(cacheSettings, cacheKey, FileSystemHttpCacheEntryType.RequestBody, request.Content, request, response: null);
            }

            await WriteRequestHeadersAsync(cacheSettings, cacheKey, request);
        }

        private static string GetFileName(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type)
        {
            switch (type)
            {
                case FileSystemHttpCacheEntryType.KeyInfo:
                    return $"{cacheKey.Key}_i{cacheKey.KeyInfoExtension}";
                case FileSystemHttpCacheEntryType.RequestHeaders:
                    return $"{cacheKey.Key}_rqh.txt";
                case FileSystemHttpCacheEntryType.RequestBody:
                    return $"{cacheKey.Key}_rqb{cacheKey.RequestBodyExtension}";
                case FileSystemHttpCacheEntryType.ResponseHeaders:
                    return $"{cacheKey.Key}_rsh.txt";
                case FileSystemHttpCacheEntryType.ResponseBody:
                    return $"{cacheKey.Key}_rsb{cacheKey.ResponseBodyExtension}";
                default:
                    throw new NotImplementedException("Unknown cache entry type: " + type);
            }
        }

        private async Task<FileSystemHttpCacheKey?> GetCacheKeyAsync(HttpRequestMessage request, FileSystemHttpCacheSettings cacheSettings)
        {
            if (request.Content is not null)
            {
                using var originalContent = request.Content;

                var requestBodyBuffer = new MemoryStream();
                await request.Content.CopyToAsync(requestBodyBuffer);
                requestBodyBuffer.Position = 0;

                request.Content = new StreamContent(requestBodyBuffer);
                foreach (var header in originalContent.Headers.NonValidated)
                {
                    request.Content.Headers.Add(header.Key, header.Value);
                }

                return await cacheSettings.GenerateKeyAsync(cacheSettings, request, requestBodyBuffer);
            }
            else
            {
                return await cacheSettings.GenerateKeyAsync(cacheSettings, request, Stream.Null);
            }
        }

        private static async Task<HttpRequestMessage?> ReadRequestFromCacheAsync(string requestHeadersPath)
        {
            if (!File.Exists(requestHeadersPath))
            {
                return null;
            }

            HttpRequestMessage request;
            using (var headersStream = File.OpenRead(requestHeadersPath))
            using (var headersReader = new StreamReader(headersStream))
            {
                var firstLine = await headersReader.ReadLineAsync();
                if (firstLine is null)
                {
                    return null;
                }
                var match = HttpRequestFirstLineRegex().Match(firstLine);
                if (!match.Success)
                {
                    return null;
                }

                request = new HttpRequestMessage(new HttpMethod(match.Groups["Method"].Value), match.Groups["Url"].Value)
                {
                    Version = new Version(match.Groups["Version"].Value),
                    Content = new StreamContent(Stream.Null),
                };

                string? line;
                bool contentHeaders = false;
                while ((line = await headersReader.ReadLineAsync()) != null)
                {
                    var pieces = line.Split(": ", count: 2);
                    if (pieces.Length != 2)
                    {
                        continue;
                    }

                    if (!contentHeaders)
                    {
                        request.Headers.TryAddWithoutValidation(pieces[0], pieces[1]);
                        contentHeaders = pieces[0] == CacheHeaderName && pieces[1] == CacheHeaderValue;
                    }
                    else
                    {
                        request.Content.Headers.TryAddWithoutValidation(pieces[0], pieces[1]);
                    }
                }
            }

            return request;
        }

        private static bool HasCachedEntry(FileSystemHttpCacheKey cacheKey)
        {
            return File.Exists(GetCacheKeyPath(cacheKey, FileSystemHttpCacheEntryType.ResponseHeaders, createDirectory: false));
        }

        private static async Task LoadResponseBodyFromCacheAsync(FileSystemHttpCacheKey cacheKey, HttpResponseMessage response, List<KeyValuePair<string, string>> contentHeaders)
        {
            var responseBodyPath = GetCacheKeyPath(cacheKey, FileSystemHttpCacheEntryType.ResponseBody, createDirectory: false);

            HttpContent content;
            if (!File.Exists(responseBodyPath))
            {
                content = new StreamContent(Stream.Null);
            }
            else
            {
                using (var bodyStream = File.OpenRead(responseBodyPath))
                {
                    var responseBodyBuffer = new MemoryStream();
                    await bodyStream.CopyToAsync(responseBodyBuffer);
                    responseBodyBuffer.Position = 0;
                    content = new StreamContent(responseBodyBuffer);
                }
            }

            foreach (var header in contentHeaders)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = content;
        }

        private static async Task<(HttpResponseMessage Response, List<KeyValuePair<string, string>> ContentHeaders)?> ReadResponseFromCacheAsync(
            HttpRequestMessage request,
            FileSystemHttpCacheKey cacheKey,
            bool disposeReplacedRequest)
        {
            var requestHeadersPath = GetCacheKeyPath(cacheKey, FileSystemHttpCacheEntryType.RequestHeaders, createDirectory: false);
            var responseHeadersPath = GetCacheKeyPath(cacheKey, FileSystemHttpCacheEntryType.ResponseHeaders, createDirectory: false);

            if (!File.Exists(responseHeadersPath))
            {
                return null;
            }

            HttpResponseMessage response;
            using (var headersStream = File.OpenRead(responseHeadersPath))
            using (var headersReader = new StreamReader(headersStream))
            {
                var firstLine = await headersReader.ReadLineAsync();
                if (firstLine is null)
                {
                    return null;
                }

                var match = HttpResponseFirstLineRegex().Match(firstLine);
                if (!match.Success)
                {
                    return null;
                }

                response = new HttpResponseMessage((HttpStatusCode)int.Parse(match.Groups["StatusCode"].Value, CultureInfo.InvariantCulture))
                {
                    RequestMessage = request,
                    Version = new Version(match.Groups["Version"].Value),
                };

                // Check the cached request for a different request URL. This indicates a redirect.
                var cachedRequest = await ReadRequestFromCacheAsync(requestHeadersPath);
                if (cachedRequest is not null && (cachedRequest.Method != request.Method || cachedRequest.RequestUri != request.RequestUri))
                {
                    if (disposeReplacedRequest)
                    {
                        request.Dispose();
                    }
                    response.RequestMessage = cachedRequest;
                }
                else
                {
                    cachedRequest?.Dispose();
                }

                if (match.Groups["ReasonPhrase"].Success)
                {
                    response.ReasonPhrase = match.Groups["ReasonPhrase"].Value;
                }

                string? line;
                var inContentHeaders = false;
                var contentHeaders = new List<KeyValuePair<string, string>>();
                while ((line = await headersReader.ReadLineAsync()) != null)
                {
                    var pieces = line.Split(": ", count: 2);
                    if (pieces.Length != 2)
                    {
                        continue;
                    }

                    if (!inContentHeaders)
                    {
                        response.Headers.TryAddWithoutValidation(pieces[0], pieces[1]);
                        inContentHeaders = pieces[0] == CacheHeaderName && pieces[1] == CacheHeaderValue;
                    }
                    else
                    {
                        contentHeaders.Add(KeyValuePair.Create(pieces[0], pieces[1]));
                    }
                }

                return (response, contentHeaders);
            }
        }

        private static async Task CacheResponseAsync(FileSystemHttpCacheSettings cacheSettings, FileSystemHttpCacheKey cacheKey, HttpResponseMessage response)
        {
            var cachePath = GetCacheKeyPath(cacheKey, FileSystemHttpCacheEntryType.ResponseBody, createDirectory: true);
            if (response.Content is not null)
            {
                using var originalContent = response.Content;

                var tempPath = GetTempPath(FileSystemHttpCacheEntryType.ResponseBody);
                try
                {
                    long contentLength;
                    using (var fileBuffer = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        await originalContent.CopyToAsync(fileBuffer);
                        fileBuffer.Flush();
                        await cacheSettings.SanitizeResponseStreamAsync(cacheKey, FileSystemHttpCacheEntryType.ResponseBody, fileBuffer, response);
                        contentLength = fileBuffer.Length;
                    }

                    if (contentLength > 0)
                    {
                        BestEffortSwap(tempPath, cachePath);

                        response.Content = new StreamContent(new FileStream(cachePath, new FileStreamOptions
                        {
                            Mode = FileMode.Open,
                            Access = FileAccess.Read,
                            Share = FileShare.Read,
                        }));
                    }
                    else
                    {
                        response.Content = new StreamContent(Stream.Null);
                    }

                    foreach (var header in originalContent.Headers.NonValidated)
                    {
                        response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                finally
                {
                    BestEffortDelete(tempPath);
                }
            }

            await WriteResponseHeadersAsync(cacheSettings, cacheKey, response);
        }

        private static async Task WriteRequestHeadersAsync(FileSystemHttpCacheSettings cacheSettings, FileSystemHttpCacheKey cacheKey, HttpRequestMessage request)
        {
            var firstLine = $"{request.Method} {request.RequestUri?.AbsoluteUri} HTTP/{request.Version}\r\n";
            var headers = request.Headers;
            var contentHeaders = request.Content?.Headers;
            await WriteHeadersAsync(cacheSettings, cacheKey, FileSystemHttpCacheEntryType.RequestHeaders, firstLine, headers, contentHeaders, request, response: null);
        }

        private static async Task WriteResponseHeadersAsync(
            FileSystemHttpCacheSettings cacheSettings,
            FileSystemHttpCacheKey cacheKey,
            HttpResponseMessage response)
        {
            var firstLine = $"HTTP/{response.Version} {(int)response.StatusCode}{(response.ReasonPhrase is null ? string.Empty : " " + response.ReasonPhrase)}\r\n";
            var headers = response.Headers;
            var contentHeaders = response.Content?.Headers;
            await WriteHeadersAsync(cacheSettings, cacheKey, FileSystemHttpCacheEntryType.ResponseHeaders, firstLine, headers, contentHeaders, request: null, response);
        }

        private static async Task WriteHeadersAsync(
            FileSystemHttpCacheSettings cacheSettings,
            FileSystemHttpCacheKey cacheKey,
            FileSystemHttpCacheEntryType type,
            string firstLine,
            HttpHeaders headers,
            HttpContentHeaders? contentHeaders,
            HttpRequestMessage? request,
            HttpResponseMessage? response)
        {
            await WriteAndSwapAsync(cacheSettings, cacheKey, type, async (fileStream) =>
            {
                using var writer = new StreamWriter(fileStream, leaveOpen: true);
                await writer.WriteAsync(firstLine);
                await AppendHeadersAsync(writer, headers);
                writer.Write($"{CacheHeaderName}: {CacheHeaderValue}\r\n");
                if (contentHeaders is not null)
                {
                    await AppendHeadersAsync(writer, contentHeaders);
                }
            }, request, response);
        }

        private static string GetCacheKeyPath(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, bool createDirectory)
        {
            var fileName = GetFileName(cacheKey, type);

            if (Path.DirectorySeparatorChar != '/')
            {
                fileName = fileName.Replace('/', Path.DirectorySeparatorChar);
            }

            var path = Path.Combine(cacheKey.Directory, fileName);

            if (createDirectory)
            {
                var directory = Path.GetDirectoryName(path);

                if (directory is not null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            return path;
        }

        private static async Task WriteContentAsync(
            FileSystemHttpCacheSettings cacheSettings,
            FileSystemHttpCacheKey cacheKey,
            FileSystemHttpCacheEntryType type,
            HttpContent content,
            HttpRequestMessage? request,
            HttpResponseMessage? response)
        {
            await WriteAndSwapAsync(cacheSettings, cacheKey, type, content.CopyToAsync, request, response);
        }

        private static async Task WriteAndSwapAsync(
            FileSystemHttpCacheSettings cacheSettings,
            FileSystemHttpCacheKey cacheKey,
            FileSystemHttpCacheEntryType type,
            Func<FileStream, Task> writeAsync,
            HttpRequestMessage? request,
            HttpResponseMessage? response)
        {
            var destination = GetCacheKeyPath(cacheKey, type, createDirectory: true);
            var destinationDir = Path.GetDirectoryName(destination);
            var tempPath = GetTempPath(type);
            try
            {
                using (var tempStream = new FileStream(tempPath, FileMode.Create))
                {
                    await writeAsync(tempStream);
                    await tempStream.FlushAsync();
                    if (request is not null)
                    {
                        await cacheSettings.SanitizeRequestStreamAsync(cacheKey, type, tempStream, request);
                    }
                    else
                    {
                        await cacheSettings.SanitizeResponseStreamAsync(cacheKey, type, tempStream, response!);
                    }
                }

                if (destinationDir is not null && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                BestEffortSwap(tempPath, destination);
            }
            finally
            {
                BestEffortDelete(tempPath);
            }
        }

        private static string GetTempPath(FileSystemHttpCacheEntryType type)
        {
            return Path.Combine(Path.GetTempPath(), $"NuGetInsights_{nameof(FileSystemHttpCacheHandler)}_{type}_{Guid.NewGuid():N}.bin");
        }

        private static void BestEffortSwap(string sourceFileName, string destFileName)
        {
            try
            {
                File.Move(sourceFileName, destFileName, overwrite: true);
            }
            catch (UnauthorizedAccessException)
            {
                // perhaps another thread completed the swap first
                if (!File.Exists(destFileName))
                {
                    throw;
                }
            }
            finally
            {
                BestEffortDelete(sourceFileName);
            }
        }

        private static void BestEffortDelete(string sourceFileName)
        {
            try
            {
                if (File.Exists(sourceFileName))
                {
                    File.Delete(sourceFileName);
                }
            }
            catch
            {
                // best effort
            }
        }

        private static async Task AppendHeadersAsync(TextWriter writer, HttpHeaders messageHeaders)
        {
            foreach (var header in messageHeaders.NonValidated.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var sanitizedValue = SensitiveHeaders.Contains(header.Key) ? "REDACTED" : header.Value.ToString();
                await writer.WriteAsync($"{header.Key}: {sanitizedValue}\r\n");
            }
        }

        [GeneratedRegex("^(?<Method>[^ ]+) (?<Url>[^ ]+) HTTP/(?<Version>.+)$")]
        private static partial Regex HttpRequestFirstLineRegex();

        [GeneratedRegex("^HTTP/(?<Version>[^ ]+) (?<StatusCode>\\d+)(?: (?<ReasonPhrase>.+))?$")]
        private static partial Regex HttpResponseFirstLineRegex();
    }
}
