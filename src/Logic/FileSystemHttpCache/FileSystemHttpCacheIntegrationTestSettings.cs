// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

#nullable enable

namespace NuGet.Insights.FileSystemHttpCache
{
    public static partial class FileSystemHttpCacheIntegrationTestSettings
    {
        public static FileSystemHttpCacheHandler Create(string? directory, FileSystemHttpCacheMode mode)
        {
            if (directory is null)
            {
                throw new ArgumentException($"The {nameof(directory)} argument is required when the file system cache is enabled. Mode: {mode}.");
            }

            var settings = new List<FileSystemHttpCacheSettings>
            {
                new FileSystemHttpCacheSettings(
                    Directory: directory,
                    IsApplicable: IsRequestApplicable,
                    GenerateKeyAsync: GenerateCacheKeyAsync,
                    SanitizeRequestStreamAsync: SanitizeRequestStreamAsync,
                    SanitizeResponseStreamAsync: SanitizeResponseStreamAsync)
            };

            return new FileSystemHttpCacheHandler(settings, mode);
        }

        public static async Task SanitizeRequestStreamAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream, HttpRequestMessage request)
        {
            await SanitizeCommonRequestHeadersAsync(cacheKey, type, stream);

            await SanitizeMsdlSasTokensAsync(cacheKey, type, stream, request);
        }

        public static async Task SanitizeResponseStreamAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream, HttpResponseMessage response)
        {
            await SanitizeCommonResponseHeadersAsync(cacheKey, type, stream);

            await SanitizeNuGetApiKeysAsync(cacheKey, type, stream, response);

            await SanitizeNotFoundAsync(cacheKey, type, stream, response);
        }

        private static async Task SanitizeCommonRequestHeadersAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream)
        {
            if (type == FileSystemHttpCacheEntryType.RequestHeaders)
            {
                var headers = await ReadStreamAsync(stream);

                // normalize URL
                headers = SanitizeUrl(headers, x => RemoveCacheBustQueryString(x.AbsoluteUri));

                // rewrite
                const string placeholderUserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights) NuGet Test Client/0.0.0 (Microsoft Windows 10.0.0) NuGet.Insights.Logic.Test.Bot/1.0.0+githash (X64; X64; .NET 0.0.0; +https://github.com/NuGet/Insights)";
                headers = Regex.Replace(headers, "\r\nUser-Agent: [^\r\n]+", $"\r\nUser-Agent: {placeholderUserAgent}", RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nX-NuGet-Client-Version: [^\r\n]+", "\r\nX-NuGet-Client-Version: 0.0.0", RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nIf-Match: (?!W\\/)\"?([^\r\n\"]+)\"?", "\r\nIf-Match: \"$1\"", RegexOptions.IgnoreCase);

                await RewriteStreamAsync(stream, headers);
            }
        }

        private static async Task SanitizeMsdlSasTokensAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream, HttpRequestMessage request)
        {
            if (type == FileSystemHttpCacheEntryType.RequestHeaders
                && (cacheKey.Key.StartsWith("https_msdl.microsoft.com/download/symbols/", StringComparison.Ordinal))
                    || request.RequestUri!.Authority.EndsWith("https://msdl.microsoft.com/download/symbols/", StringComparison.Ordinal))
            {
                var headers = await ReadStreamAsync(stream);
                var sanitizedHeaders = SanitizeUrl(headers, x => x.Obfuscate());
                if (sanitizedHeaders != headers)
                {
                    await RewriteStreamAsync(stream, sanitizedHeaders);
                }
            }
        }

        private static string SanitizeUrl(string headers, Func<Uri, string> sanitize)
        {
            var endOfFirstLine = headers.IndexOf('\n', StringComparison.Ordinal);
            var firstLine = headers.Substring(0, endOfFirstLine);
            var pieces = firstLine.Split([' ']);
            if (Uri.TryCreate(pieces[1], UriKind.Absolute, out var parsedUri))
            {
                var sanitized = sanitize(parsedUri);
                if (sanitized != pieces[1])
                {
                    pieces[1] = sanitized;
                    firstLine = string.Join(" ", pieces);
                    return string.Concat(firstLine, headers.AsSpan(endOfFirstLine));
                }
            }

            return headers;
        }

        private static async Task SanitizeCommonResponseHeadersAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream)
        {
            if (type == FileSystemHttpCacheEntryType.ResponseHeaders)
            {
                var headers = await ReadStreamAsync(stream);

                // rewrite
                headers = Regex.Replace(headers, "\r\nAccess-Control-Expose-Headers: [^\r\n]+", "\r\nAccess-Control-Expose-Headers: x-ms-request-id,Server,x-ms-version,Content-Type,Cache-Control,Last-Modified,ETag,x-ms-lease-status,x-ms-blob-type,Content-Length,Date,Transfer-Encoding", RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nCache-Control: [^\r\n]+", "\r\nCache-Control: no-store", RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nDate: [^\r\n]+", $"\r\nDate: Wed, 1 Jan 2020 00:00:00 GMT", RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nETag: (?!W\\/)\"?([^\r\n\"]+)\"?", "\r\nETag: \"$1\"", RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nExpires: [^\r\n]+", $"\r\nExpires: Wed, 1 Jan 2020 00:00:00 GMT", RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nx-ms-request-id: [^\r\n]+", "\r\nx-ms-request-id: 00000000-0000-0000-0000-000000000000", RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nx-ms-version: [^\r\n]+", "\r\nx-ms-version: 2013-08-15", RegexOptions.IgnoreCase);

                // remove
                headers = Regex.Replace(headers, "\r\nAkamai-GRN: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nAccept-Ranges: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nContent-Language: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nContent-MD5: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nPragma: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nTransfer-Encoding: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nVary: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nx-azure-ref: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nX-Cache: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nX-Cache-Info: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nX-CDN[^:]+: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nx-fd-int-roxy-purgeid: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);
                headers = Regex.Replace(headers, "\r\nx-ms-lease-state: [^\r\n]+", string.Empty, RegexOptions.IgnoreCase);

                await RewriteStreamAsync(stream, headers);
            }
        }

        private static async Task SanitizeNuGetApiKeysAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream, HttpResponseMessage response)
        {
            if (type == FileSystemHttpCacheEntryType.ResponseBody
                && response.RequestMessage!.RequestUri!.AbsoluteUri == "https://api.nuget.org/v3/catalog0/page8643.json")
            {
                var json = await ReadStreamAsync(stream);
                var sanitizedJson = Regex.Replace(json, @"oy2[\w]{43}", new string('x', 46));
                if (json == sanitizedJson)
                {
                    throw new InvalidOperationException($"Expected the JSON of cache key {cacheKey.Key} to change after sanitizing.");
                }

                var bytes = Encoding.UTF8.GetBytes(sanitizedJson);
                if (bytes.Length != stream.Length)
                {
                    throw new InvalidOperationException($"Expected the length of the stream to not change after sanitizing.");
                }

                stream.Position = 0;
                await stream.WriteAsync(bytes, 0, bytes.Length, CancellationToken.None);
            }
        }

        private static async Task SanitizeNotFoundAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream, HttpResponseMessage response)
        {
            if (type == FileSystemHttpCacheEntryType.ResponseBody
                && response.StatusCode == HttpStatusCode.NotFound
                && response.Content.Headers.ContentType?.MediaType == "application/xml")
            {
                var xml = await ReadStreamAsync(stream);
                xml = Regex.Replace(xml, "RequestId:(\\s*)[^\\s<>]+", "RequestId:00000000-0000-0000-0000-000000000000", RegexOptions.IgnoreCase);
                xml = Regex.Replace(xml, "Time:(\\s*)[^\\s<>]+", "Time:2020-01-01T00:00:00.0000000Z", RegexOptions.IgnoreCase);

                await RewriteStreamAsync(stream, xml);
            }
        }

        private static async Task<string> ReadStreamAsync(Stream stream)
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private static async Task RewriteStreamAsync(Stream stream, string newContent)
        {
            stream.Position = 0;
            var bytes = Encoding.UTF8.GetBytes(newContent);
            await stream.WriteAsync(bytes, 0, bytes.Length, CancellationToken.None);
            if (bytes.Length < stream.Length)
            {
                stream.SetLength(bytes.Length);
            }
        }

        /// <summary>
        /// Determine which requests should be cached to the file system HTTP cache. If true is returned, it is cacheable. If false is returned, it is not cacheable.
        /// If an exception is thrown, the request should be aborted and the caller should fail.
        /// </summary>
        public static bool IsRequestApplicable(HttpRequestMessage request)
        {
            if (request.RequestUri is null)
            {
                throw new NotSupportedException($"Request URI is required for file system caching. Request: {request.Method} {request.RequestUri?.AbsoluteUri}");
            }

            var authority = request.RequestUri.Authority;
            if (authority.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                // Azure Blob Storage is never cached.
                return false;
            }

            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
            {
                throw new NotSupportedException($"Only GET and HEAD requests are accepted for file system caching. Request: {request.Method} {request.RequestUri?.AbsoluteUri}");
            }

            if (request.RequestUri.Scheme != Uri.UriSchemeHttp && request.RequestUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new NotSupportedException($"Only HTTP and HTTPS requests are accepted for file system caching. Request: {request.Method} {request.RequestUri?.AbsoluteUri}");
            }

            if (authority != "api.nuget.org"
                && authority != "globalcdn.nuget.org"
                && authority != "msdl.microsoft.com"
                && authority != "nuget.org")
            {
                throw new NotSupportedException($"The host name is not allowed for file system caching. Request: {request.Method} {request.RequestUri?.AbsoluteUri}");
            }

            if (request.Content is not null)
            {
                throw new NotSupportedException($"Request content is not allowed for file system caching. Request: {request.Method} {request.RequestUri?.AbsoluteUri}");
            }

            return true;
        }

        /// <summary>
        /// These headers are highly variable and mess up cache key generation.
        /// </summary>
        private static FrozenSet<string> IgnoredHeaders = new[]
        {
            "User-Agent",
            "X-NuGet-Client-Version",
            "Accept-Encoding"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        private static FrozenSet<string> BannedFileExtensions = new[]
        {
            ".nupkg",
            ".snupkg",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        public static Task<FileSystemHttpCacheKey?> GenerateCacheKeyAsync(FileSystemHttpCacheSettings settings, HttpRequestMessage request, Stream requestBody)
        {
            using var memoryStream = new MemoryStream();
            using (var jsonWriter = new Utf8JsonWriter(memoryStream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
            {
                // loosely follow the HAR format
                jsonWriter.WriteStartObject();
                jsonWriter.WriteString("method", request.Method.ToString().ToUpperInvariant());

                // the cache bust behavior is flaky, so remove it from the URL
                var requestUri = request.RequestUri!.AbsoluteUri;
                if (!string.IsNullOrEmpty(request.RequestUri.Query))
                {
                    requestUri = RemoveCacheBustQueryString(requestUri);
                }

                jsonWriter.WriteString("url", requestUri);

                WriteHeaders(jsonWriter, request.Headers, request.Content?.Headers);

                jsonWriter.WriteEndObject();
            }

            memoryStream.Position = 0;

            var (path, isPathLossy) = AbbreviatePath(request);

            var fileNamePieces = new List<string>
            {
                $"{request.RequestUri.Scheme}_{request.RequestUri.Authority}",
                path,
                $"/{request.Method.ToString().ToUpperInvariant()[0]}_"
            };

            if (request.Headers.Range is not null)
            {
                fileNamePieces.Add("R_");
            }

            // The cache key will include the request URL, so take only part of the hash to avoid max path issues.
            var hash = SHA512.HashData(memoryStream).Take(isPathLossy ? 16 : 4).ToArray().ToTrimmedBase32();
            fileNamePieces.Add(hash);

            var cacheKey = string.Join(string.Empty, fileNamePieces);
            var buffer = memoryStream.GetBuffer();

            var requestBodyExtension = request.Content?.Headers.ContentType?.MediaType == "application/json" ? ".json" : ".bin";
            var requestUriExtension = Path.GetExtension(request.RequestUri.AbsolutePath);
            var responseBodyExtension = string.IsNullOrEmpty(requestUriExtension) || BannedFileExtensions.Contains(requestUriExtension) ? $"{requestUriExtension}.bin" : requestUriExtension;

            return Task.FromResult<FileSystemHttpCacheKey?>(new FileSystemHttpCacheKey(
                Directory: settings.Directory,
                Key: cacheKey,
                Encoding.UTF8.GetString(buffer, 0, (int)memoryStream.Length),
                KeyInfoExtension: ".json",
                RequestBodyExtension: requestBodyExtension,
                ResponseBodyExtension: responseBodyExtension));
        }

        private static string RemoveCacheBustQueryString(string requestUri)
        {
            return RemoveCacheBustQueryString().Replace(requestUri, "$1").TrimEnd(['?', '&']);
        }

        private static (string Path, bool Lossy) AbbreviatePath(HttpRequestMessage request)
        {
            string path = request.RequestUri!.AbsolutePath;

            // remove redundant ID and version components from the package content URL
            if (request.RequestUri.Authority == "api.nuget.org"
                && path.StartsWith("/v3-flatcontainer/", StringComparison.Ordinal)
                && request.RequestUri.Segments.Length == 5)
            {
                var lastFileNamePart = request.RequestUri.Segments[4].Split('.').Last();
                path = string.Join(string.Empty, request.RequestUri.Segments.Take(4)) + lastFileNamePart;
            }

            if (request.RequestUri.Authority == "msdl.microsoft.com"
                && path.StartsWith("/download/symbols/", StringComparison.Ordinal)
                && request.RequestUri.Segments.Length == 6)
            {
                path = string.Join(string.Empty, request.RequestUri.Segments.Take(4)) + request.RequestUri.Segments[4].TrimEnd('/');
            }

            return (path, false);
        }

        private static void WriteHeaders(Utf8JsonWriter jsonWriter, params HttpHeaders?[] headers)
        {
            var anyHeaders = false;
            var sortedHeaders = headers
                .Where(x => x is not null)
                .SelectMany(x => x!.NonValidated)
                .OrderBy(x => x.Key, StringComparer.Ordinal);

            foreach (var header in sortedHeaders)
            {
                if (IgnoredHeaders.Contains(header.Key))
                {
                    continue;
                }

                if (!anyHeaders)
                {
                    jsonWriter.WritePropertyName("headers");
                    jsonWriter.WriteStartArray();
                    anyHeaders = true;
                }

                jsonWriter.WriteStartObject();
                jsonWriter.WriteString("name", header.Key.ToLowerInvariant());

                var normalizedValue = header.Value.ToString();
                if (header.Key.Equals("If-Match", StringComparison.OrdinalIgnoreCase)
                    && !normalizedValue.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
                    && !normalizedValue.StartsWith("\"", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedValue = $"\"{normalizedValue}\"";
                }

                jsonWriter.WriteString("value", normalizedValue);
                jsonWriter.WriteEndObject();
            }

            if (anyHeaders)
            {
                jsonWriter.WriteEndArray();
            }
        }

        [GeneratedRegex("(\\?|&)cache-bust=[^&]+&?")]
        private static partial Regex RemoveCacheBustQueryString();
    }
}
