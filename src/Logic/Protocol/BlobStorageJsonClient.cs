// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Azure;
using Azure.Storage.Blobs.Models;

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
        private readonly RedirectResolver _redirectResolver;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IThrottle _throttle;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<BlobStorageJsonClient> _logger;

        public BlobStorageJsonClient(
            HttpClient httpClient,
            RedirectResolver redirectResolver,
            ServiceClientFactory serviceClientFactory,
            IThrottle throttle,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsSettings> options,
            ILogger<BlobStorageJsonClient> logger)
        {
            _httpClient = httpClient;
            _redirectResolver = redirectResolver;
            _serviceClientFactory = serviceClientFactory;
            _throttle = throttle;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
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
            DownloadResult? result = null;
            try
            {
                result = await GetResponseAsync(endpoint.Url, head: true);

                var age = DateTimeOffset.UtcNow - result.LastModified;
                _telemetryClient.TrackMetric(
                    nameof(BlobStorageJsonClient) + "." + nameof(GetAsOfTimestampAsync) + ".AgeMinutes",
                    age.TotalMinutes,
                    new Dictionary<string, string>
                    {
                        { "Url", endpoint.Url },
                        { "DownloadMethod", result.Method.ToString() },
                        { "LastUrl", result.LastUrl.AbsoluteUri },
                    });

                return (endpoint, result.LastModified);
            }
            finally
            {
                result?.Dispose();
                _throttle.Release();
            }
        }

        private async Task<AsOfData<T>> DownloadAsync<T>(string url, Func<Stream, IAsyncEnumerable<T>> deserialize)
        {
            var result = await GetResponseAsync(url, head: false);
            return new AsOfData<T>(
                result.LastModified,
                url,
                result.ETag,
                AsyncEnumerableEx.Using(
                    () => new ThrottledDisposable(result, _throttle),
                    _ => deserialize(result.Stream!)));
        }

        private async Task<DownloadResult> GetResponseAsync(string url, bool head)
        {
            try
            {
                await _throttle.WaitAsync();

                if (_options.Value.UseBlobClientForExternalData != false)
                {
                    try
                    {
                        var result = await GetResponseWithBlobClientAsync(url, head);
                        if (result is not null)
                        {
                            return result;
                        }
                    }
                    catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Unauthorized || ex.Status == (int)HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning(
                            ex,
                            "Blob client request to {Url} failed with status code {StatusCode}. Trying without authorization.",
                            url,
                            ex.Status);
                    }
                }

                return await GetResponseWithHttpClientAsync(url, head);
            }
            catch
            {
                _throttle.Release();
                throw;
            }
        }

        public enum DownloadMethod
        {
            HttpClient = 1,
            BlobClient,
        }

        private class DownloadResult : IDisposable
        {
            public DownloadResult(DownloadMethod method, Uri lastUrl, IDisposable disposable, Stream stream, DateTimeOffset lastModified, string etag)
            {
                Method = method;
                LastUrl = lastUrl;
                Disposable = disposable;
                Stream = stream;
                LastModified = lastModified;
                ETag = etag;
            }

            public DownloadResult(DownloadMethod method, Uri lastUrl, DateTimeOffset lastModified, string etag)
            {
                Method = method;
                LastUrl = lastUrl;
                LastModified = lastModified;
                ETag = etag;
            }

            public DownloadMethod Method { get; }
            public Uri LastUrl { get; }
            public IDisposable? Disposable { get; }
            public Stream? Stream { get; }
            public DateTimeOffset LastModified { get; }
            public string ETag { get; }

            public void Dispose()
            {
                Stream?.Dispose();
                Disposable?.Dispose();
            }
        }

        private async Task<DownloadResult?> GetResponseWithBlobClientAsync(string url, bool head)
        {
            var storageTokenCredential = await _serviceClientFactory.GetStorageTokenCredentialAsync();
            if (storageTokenCredential is null)
            {
                if (_options.Value.UseBlobClientForExternalData == true)
                {
                    var storageCredentialType = await _serviceClientFactory.GetStorageCredentialTypeAsync();
                    throw new InvalidOperationException($"The {nameof(NuGetInsightsSettings.UseBlobClientForExternalData)} setting is only supported when a storage token credential is used. The storage credential type is {storageCredentialType}.");
                }

                return null;
            }

            var lastUrl = await _redirectResolver.FollowRedirectsAsync(url);
            if (lastUrl.Scheme != "https" || !IsAzureBlobStorage(lastUrl))
            {
                return null;
            }

            var blobClient = await _serviceClientFactory.GetBlobClientAsync(lastUrl);

            if (head)
            {
                BlobProperties properties = await blobClient.GetPropertiesAsync();
                return new DownloadResult(DownloadMethod.BlobClient, lastUrl, properties.LastModified, properties.ETag.ToString());
            }
            else
            {
                BlobDownloadStreamingResult? result = null;
                try
                {
                    result = await blobClient.DownloadStreamingAsync();
                    return new DownloadResult(DownloadMethod.BlobClient, lastUrl, result, result.Content, result.Details.LastModified, result.Details.ETag.ToString());
                }
                catch
                {
                    result?.Dispose();
                    throw;
                }
            }

        }

        private static bool IsAzureBlobStorage(Uri lastUrl)
        {
            return lastUrl.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<DownloadResult> GetResponseWithHttpClientAsync(string url, bool head)
        {
            HttpResponseMessage? response = null;
            try
            {
                var method = head ? HttpMethod.Head : HttpMethod.Get;
                var completionOption = head ? HttpCompletionOption.ResponseContentRead : HttpCompletionOption.ResponseHeadersRead;
                var request = new HttpRequestMessage(method, url);

                // Prior to this version, Azure Blob Storage did not put quotes around etag headers...
                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

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
                var stream = await response.Content.ReadAsStreamAsync();
                var lastUrl = response.RequestMessage?.RequestUri ?? request.RequestUri!;

                return new DownloadResult(DownloadMethod.HttpClient, lastUrl, response, stream, asOfTimestamp, etag);
            }
            catch
            {
                response?.Dispose();
                throw;
            }
        }
    }
}
