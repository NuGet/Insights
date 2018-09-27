using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public static class HttpSourceExtensions
    {
        private const int DefaultMaxTries = 3;

        private static readonly JsonSerializer Serializer = new JsonSerializer
        {
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new AssumeUniversalDateTimeOffsetConverter(),
            },
        };

        public static async Task<bool> UrlExistsAsync(this HttpSource httpSource, string url, ILogger logger)
        {
            var nuGetLogger = logger.ToNuGetLogger();
            return await httpSource.ProcessResponseAsync(
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
                nuGetLogger,
                CancellationToken.None);
        }

        public static Task<T> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            bool ignoreNotFounds,
            ILogger logger)
        {
            return httpSource.DeserializeUrlAsync<T>(
                url,
                ignoreNotFounds,
                maxTries: DefaultMaxTries,
                serializer: Serializer,
                logger: logger);
        }

        public static Task<T> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            bool ignoreNotFounds,
            int maxTries,
            ILogger logger)
        {
            return httpSource.DeserializeUrlAsync<T>(
                url,
                ignoreNotFounds,
                maxTries,
                serializer: Serializer,
                logger: logger);
        }

        public static async Task<T> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            bool ignoreNotFounds,
            int maxTries,
            JsonSerializer serializer,
            ILogger logger)
        {
            var nuGetLogger = logger.ToNuGetLogger();
            return await httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, nuGetLogger)
                {
                    IgnoreNotFounds = ignoreNotFounds,
                    MaxTries = maxTries,
                    RequestTimeout = TimeSpan.FromSeconds(30),
                },
                stream =>
                {
                    if (stream == null)
                    {
                        return Task.FromResult(default(T));
                    }

                    using (var textReader = new StreamReader(stream))
                    using (var jsonReader = new JsonTextReader(textReader))
                    {
                        var result = serializer.Deserialize<T>(jsonReader);
                        return Task.FromResult(result);
                    }
                },
                nuGetLogger,
                CancellationToken.None);
        }

        public static async Task<BlobMetadata> GetBlobMetadataAsync(this HttpSource httpSource, string url, ILogger logger)
        {
            var nuGetLogger = logger.ToNuGetLogger();

            // Try to get all of the information using a HEAD request.
            var blobMetadata = await httpSource.ProcessResponseAsync(
                new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Head, url, nuGetLogger)),
                response =>
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return Task.FromResult(new BlobMetadata(
                            exists: false,
                            hasContentMD5Header: false,
                            contentMD5: null));
                    }

                    response.EnsureSuccessStatusCode();

                    var headerMD5Bytes = response.Content.Headers.ContentMD5;
                    if (headerMD5Bytes != null)
                    {
                        var contentMD5 = BytesToHex(headerMD5Bytes);
                        return Task.FromResult(new BlobMetadata(
                            exists: true,
                            hasContentMD5Header: true,
                            contentMD5: contentMD5));
                    }

                    return Task.FromResult<BlobMetadata>(null);
                },
                nuGetLogger,
                CancellationToken.None);

            if (blobMetadata != null)
            {
                return blobMetadata;
            }

            // If no Content-MD5 header was found in the response, calculate the package hash by downloading the
            // package.
            return await httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, nuGetLogger),
                async stream =>
                {
                    var buffer = new byte[16 * 1024];
                    using (var md5 = new MD5IncrementalHash())
                    {
                        int read;
                        do
                        {
                            read = await stream.ReadAsync(buffer, 0, buffer.Length);
                            md5.AppendData(buffer, 0, read);
                        }
                        while (read > 0);

                        var hash = md5.GetHashAndReset();
                        var contentMD5 = BytesToHex(hash);
                        return new BlobMetadata(
                            exists: true,
                            hasContentMD5Header: false,
                            contentMD5: contentMD5);
                    }
                },
                nuGetLogger,
                CancellationToken.None);
        }

        private static string BytesToHex(byte[] hash)
        {
            return BitConverter
                .ToString(hash)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
