// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;

#nullable enable

namespace NuGet.Insights
{
    public static class HttpSourceExtensions
    {
        private const int DefaultMaxTries = 3;

        internal static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            Converters =
            {
                new AssumeUniversalDateTimeConverter(),
                new AssumeUniversalDateTimeOffsetConverter(),
            },
        };

        public static async Task<bool> UrlExistsAsync(
            this HttpSource httpSource,
            string url,
            ILogger logger,
            CancellationToken token = default)
        {
            var nuGetLogger = logger.ToNuGetLogger();
            return await httpSource.ProcessResponseWithRetryAsync(
                new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Head, url, nuGetLogger))
                {
                    IgnoreNotFounds = true,
                },
                response =>
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return Task.FromResult(true);
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return Task.FromResult(false);
                    }

                    throw new HttpRequestException(
                        $"The request to {url} return HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                },
                logger,
                token);
        }

        public static async Task<T> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            ILogger logger,
            CancellationToken token = default) where T : notnull
        {
            return await httpSource.DeserializeUrlAsync<T>(
                url,
                maxTries: DefaultMaxTries,
                logger: logger,
                token: token);
        }

        public static async Task<T> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            int maxTries,
            ILogger logger,
            CancellationToken token = default) where T : notnull
        {
            return (await httpSource.DeserializeUrlAsync<T>(
                url,
                ignoreNotFounds: false,
                maxTries: maxTries,
                logger: logger,
                token: token))!;
        }

        public static async Task<T?> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            bool ignoreNotFounds,
            ILogger logger,
            CancellationToken token = default) where T : notnull
        {
            return await httpSource.DeserializeUrlAsync<T>(
                url,
                ignoreNotFounds,
                maxTries: DefaultMaxTries,
                logger: logger,
                token: token);
        }


        public static async Task<T?> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            bool ignoreNotFounds,
            int maxTries,
            ILogger logger,
            CancellationToken token = default) where T : notnull
        {
            return await httpSource.DeserializeUrlAsync<T>(
                url,
                ignoreNotFounds,
                maxTries: maxTries,
                options: JsonSerializerOptions,
                logger: logger,
                token: token);
        }


        public static async Task<T?> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            bool ignoreNotFounds,
            int maxTries,
            JsonSerializerOptions options,
            ILogger logger,
            CancellationToken token = default) where T : notnull
        {
            var nuGetLogger = logger.ToNuGetLogger();
            return await httpSource.ProcessStreamWithRetryAsync(
                new HttpSourceRequest(url, nuGetLogger)
                {
                    IgnoreNotFounds = ignoreNotFounds,
                    MaxTries = maxTries,
                    RequestTimeout = TimeSpan.FromSeconds(30),
                },
                async stream =>
                {
                    if (stream == null)
                    {
                        return default;
                    }

                    T? result;
                    try
                    {
                        result = await JsonSerializer.DeserializeAsync<T>(stream, options, token);
                    }
                    catch (JsonException ex)
                    {
                        logger.LogWarning(ex, "Unable to deserialize {Url} as type {TypeName}.", url, typeof(T).FullName);
                        throw;
                    }

                    if (result is null)
                    {
                        logger.LogWarning("Deserialization of {Url} as type {TypeName} resulted in null.", url, typeof(T).FullName);
                        throw new InvalidOperationException("Deserialization of a URL unexpectedly resulted in null.");
                    }

                    return result;
                },
                logger,
                token);
        }

        public static async Task<T> ProcessStreamWithRetryAsync<T>(
            this HttpSource httpSource,
            HttpSourceRequest request,
            Func<Stream, Task<T>> processAsync,
            ILogger logger,
            CancellationToken token)
        {
            var nuGetLogger = logger.ToNuGetLogger();
            var attempt = 0;
            while (true)
            {
                var fetchedHeaders = false;
                attempt++;
                try
                {
                    return await httpSource.ProcessStreamAsync(
                        request,
                        async stream =>
                        {
                            fetchedHeaders = true;
                            return await processAsync(stream);
                        },
                        nuGetLogger,
                        token);
                }
                catch (Exception ex) when (ShouldRetryStreamException(attempt, fetchedHeaders, ex, token))
                {
                    logger.LogTransientWarning(ex, "On attempt {Attempt}, processing the stream response body failed. Trying again.", attempt);
                }
            }
        }

        public static async Task<T> ProcessResponseWithRetryAsync<T>(
            this HttpSource httpSource,
            HttpSourceRequest request,
            Func<HttpResponseMessage, Task<T>> processAsync,
            ILogger logger,
            CancellationToken token)
        {
            var nuGetLogger = logger.ToNuGetLogger();
            var attempt = 0;
            while (true)
            {
                var fetchedHeaders = false;
                string? url = null;
                attempt++;
                try
                {
                    return await httpSource.ProcessResponseAsync(
                        request,
                        async (response) =>
                        {
                            url = response.RequestMessage?.RequestUri?.AbsoluteUri;
                            fetchedHeaders = true;
                            return await processAsync(response);
                        },
                        nuGetLogger,
                        token);
                }
                catch (Exception ex) when (ShouldRetryStreamException(attempt, fetchedHeaders, ex, token))
                {
                    logger.LogTransientWarning(ex, "On attempt {Attempt}, processing the response body for {Url} failed. Trying again.", attempt, url);
                }
            }
        }

        private static bool ShouldRetryStreamException(int attempt, bool fetchedHeaders, Exception ex, CancellationToken token)
        {
            return attempt < 3
                && fetchedHeaders
                && !token.IsCancellationRequested
                && (ex is IOException
                    || ex is OperationCanceledException
                    || (ex is HttpRequestException && ex.InnerException is IOException));
        }

        public static async Task<BlobMetadata> GetBlobMetadataAsync(
            this HttpSource httpSource,
            string url,
            ILogger logger,
            CancellationToken token = default)
        {
            var nuGetLogger = logger.ToNuGetLogger();

            // Try to get all of the information using a HEAD request.
            var blobMetadata = await httpSource.ProcessResponseWithRetryAsync(
                new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Head, url, nuGetLogger)),
                response =>
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return Task.FromResult<BlobMetadata?>(new BlobMetadata(
                            exists: false,
                            hasContentMD5Header: false,
                            contentMD5: null));
                    }

                    response.EnsureSuccessStatusCode();

                    var headerMD5Bytes = response.Content.Headers.ContentMD5;
                    if (headerMD5Bytes != null)
                    {
                        var contentMD5 = headerMD5Bytes.ToLowerHex();
                        return Task.FromResult<BlobMetadata?>(new BlobMetadata(
                            exists: true,
                            hasContentMD5Header: true,
                            contentMD5: contentMD5));
                    }

                    return Task.FromResult<BlobMetadata?>(null);
                },
                logger,
                token);

            if (blobMetadata != null)
            {
                return blobMetadata;
            }

            // If no Content-MD5 header was found in the response, calculate the package hash by downloading the
            // package.
            return await httpSource.ProcessStreamWithRetryAsync(
                new HttpSourceRequest(url, nuGetLogger),
                async stream =>
                {
                    var buffer = new byte[16 * 1024];
                    using var md5 = MD5.Create();
                    int read;
                    do
                    {
                        read = await stream.ReadAsync(buffer);
                        md5.TransformBlock(buffer, 0, read, buffer, 0);
                    }
                    while (read > 0);

                    md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    var contentMD5 = md5.Hash!.ToLowerHex();

                    return new BlobMetadata(
                        exists: true,
                        hasContentMD5Header: false,
                        contentMD5: contentMD5);
                },
                logger,
                token);
        }
    }
}
