// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public static async Task<T> DeserializeUrlAsync<T>(
            this HttpSource httpSource,
            string url,
            ILogger logger,
            CancellationToken token = default) where T : notnull
        {
            return (await httpSource.DeserializeUrlAsync<T>(
                url,
                ignoreNotFounds: false,
                maxTries: DefaultMaxTries,
                options: JsonSerializerOptions,
                logger: logger,
                token: token))!;
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
            return await httpSource.ProcessResponseWithRetryAsync(
                new HttpSourceRequest(url, nuGetLogger)
                {
                    IgnoreNotFounds = ignoreNotFounds,
                    MaxTries = maxTries,
                    RequestTimeout = TimeSpan.FromSeconds(30),
                },
                async response =>
                {
                    if (ignoreNotFounds && response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return default;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(token);

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
                            url = response.RequestMessage?.RequestUri?.Obfuscate();
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
    }
}
