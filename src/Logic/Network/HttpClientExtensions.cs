// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public static class HttpClientExtensions
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
            this HttpClient httpClient,
            string url,
            ILogger logger,
            CancellationToken token = default) where T : notnull
        {
            return await httpClient.ProcessResponseWithRetriesAsync(
                () => new HttpRequestMessage(HttpMethod.Get, url),
                async response =>
                {
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync(token);

                    T? result;
                    try
                    {
                        result = await JsonSerializer.DeserializeAsync<T>(stream, JsonSerializerOptions, token);
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

        public static async Task<T> ProcessResponseWithRetriesAsync<T>(
            this HttpClient httpClient,
            Func<HttpRequestMessage> getRequest,
            Func<HttpResponseMessage, Task<T>> processAsync,
            ILogger logger,
            CancellationToken token)
        {
            var attempt = 0;
            while (true)
            {
                var fetchedHeaders = false;
                string? url = null;
                attempt++;
                try
                {
                    using var request = getRequest();
                    url = request.RequestUri?.Obfuscate();
                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                    fetchedHeaders = true;

                    return await processAsync(response);
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
