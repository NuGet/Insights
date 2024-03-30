// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public record BlobStorageJsonEndpoint<T>(
        string Url,
        TimeSpan AgeLimit,
        string Name,
        Func<Stream, IAsyncEnumerable<T>> Deserialize);

    public class BlobStorageJsonClient
    {
        private readonly HttpClient _httpClient;
        private readonly IThrottle _throttle;
        private readonly ITelemetryClient _telemetryClient;

        public BlobStorageJsonClient(HttpClient httpClient, IThrottle throttle, ITelemetryClient telemetryClient)
        {
            _httpClient = httpClient;
            _throttle = throttle;
            _telemetryClient = telemetryClient;
        }

        public async Task<AsOfData<T>> DownloadNewestAsync<T>(IReadOnlyCollection<BlobStorageJsonEndpoint<T>> endpoints, string generalName)
        {
            if (endpoints is null || endpoints.Count == 0)
            {
                throw new InvalidOperationException($"At least one {generalName} URL is required.");
            }

            var timestampTasks = endpoints.Select(GetAsOfTimestampAsync).ToList();
            var timestamps = await Task.WhenAll(timestampTasks);
            var latest = timestamps.MaxBy(x => x.AsOfTimestamp)!;
            if (DateTimeOffset.UtcNow - latest.AsOfTimestamp > latest.Endpoint.AgeLimit)
            {
                throw new InvalidOperationException(
                    $"The last modified {latest.Endpoint.Name} URL is {latest.Endpoint.Url} " +
                    $"(modified on {latest.AsOfTimestamp:O}) " +
                    $"but this is older than the age limit of {latest.Endpoint.AgeLimit}. " +
                    $"Check for stale data or bad configuration.");
            }

            return await DownloadAsync(latest.Endpoint.Url, latest.Endpoint.Deserialize);
        }

        public async Task<AsOfData<T>> DownloadNewestAsync<T>(IReadOnlyCollection<string> urls, TimeSpan ageLimit, string name, Func<Stream, IAsyncEnumerable<T>> deserialize)
        {
            var endpoints = urls
                .Select(x => new BlobStorageJsonEndpoint<T>(x, ageLimit, name, deserialize))
                .ToList();
            return await DownloadNewestAsync(endpoints, name);
        }

        private async Task<(BlobStorageJsonEndpoint<T> Endpoint, DateTimeOffset AsOfTimestamp)> GetAsOfTimestampAsync<T>(BlobStorageJsonEndpoint<T> endpoint)
        {
            HttpResponseMessage? response = null;
            try
            {
                DateTimeOffset asOfTimestamp;
                (response, asOfTimestamp, _) = await GetResponseAsync(HttpMethod.Head, endpoint.Url, HttpCompletionOption.ResponseContentRead);

                var age = DateTimeOffset.UtcNow - asOfTimestamp;

                _telemetryClient.TrackMetric(
                    nameof(BlobStorageJsonClient) + "." + nameof(GetAsOfTimestampAsync) + ".AgeMinutes",
                    age.TotalMinutes,
                    new Dictionary<string, string>
                    {
                        { "Url", endpoint.Url },
                    });

                return (endpoint, asOfTimestamp);
            }
            finally
            {
                _throttle.Release();
                response?.Dispose();
            }
        }

        private async Task<AsOfData<T>> DownloadAsync<T>(string url, Func<Stream, IAsyncEnumerable<T>> deserialize)
        {
            HttpResponseMessage? response = null;
            try
            {
                DateTimeOffset asOfTimestamp;
                string etag;
                (response, asOfTimestamp, etag) = await GetResponseAsync(HttpMethod.Get, url, HttpCompletionOption.ResponseHeadersRead);

                var stream = await response.Content.ReadAsStreamAsync();

                return new AsOfData<T>(
                    asOfTimestamp,
                    url,
                    etag,
                    AsyncEnumerableEx.Using(
                        () => new ResponseAndThrottle(response, _throttle),
                        _ => deserialize(stream)));
            }
            catch
            {
                response?.Dispose();
                _throttle.Release();
                throw;
            }
        }

        private async Task<(HttpResponseMessage Message, DateTimeOffset LastModified, string ETag)> GetResponseAsync(
            HttpMethod method,
            string url,
            HttpCompletionOption completionOption)
        {
            HttpResponseMessage? response = null;
            try
            {
                var request = new HttpRequestMessage(method, url);

                // Prior to this version, Azure Blob Storage did not put quotes around etag headers...
                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                await _throttle.WaitAsync();
                response = await _httpClient.SendAsync(request, completionOption);

                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.LastModified is null)
                {
                    throw new InvalidOperationException($"No Last-Modified header was returned for URL {url}.");
                }

                if (response.Headers.ETag is null)
                {
                    throw new InvalidOperationException($"No ETag header was returned for URL {url}.");
                }

                var asOfTimestamp = response.Content.Headers.LastModified.Value.ToUniversalTime();
                var etag = response.Headers.ETag.ToString();

                return (response, asOfTimestamp, etag);
            }
            catch
            {
                response?.Dispose();
                _throttle.Release();
                throw;
            }
        }
    }
}
