// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace NuGet.Insights
{
    public record BlobStorageJsonEndpoint<T>(
        Uri Url,
        TimeSpan AgeLimit,
        string Name,
        Func<Stream, IAsyncEnumerable<IReadOnlyList<T>>> Deserialize);

    public enum BlobRequestMethod
    {
        Head = 1,
        Get,
    }

    public enum BlobClientType
    {
        HttpClient = 1,
        BlobClient,
    }

    public record ExternalBlobRequest(
        BlobRequestMethod Method,
        Uri Url,
        bool UseThrottle,
        bool AllowNotFound);

    public class ExternalBlobResponse : IDisposable
    {
        public ExternalBlobResponse(
            ExternalBlobRequest request,
            BlobClientType clientType,
            Uri lastUrl,
            IReadOnlyList<IDisposable> disposables,
            Stream stream,
            DateTimeOffset lastModified,
            string etag,
            ILookup<string, string> headers)
        {
            Request = request;
            ClientType = clientType;
            LastUrl = lastUrl;
            Disposables = disposables;
            Stream = stream;
            LastModified = lastModified;
            ETag = etag;
            Headers = headers;
        }

        public ExternalBlobResponse(
            ExternalBlobRequest request,
            BlobClientType clientType,
            Uri lastUrl,
            DateTimeOffset lastModified,
            string etag,
            ILookup<string, string> headers)
        {
            Request = request;
            ClientType = clientType;
            LastUrl = lastUrl;
            Disposables = Array.Empty<IDisposable>();
            LastModified = lastModified;
            ETag = etag;
            Headers = headers;
        }

        public ExternalBlobRequest Request { get; }
        public BlobClientType ClientType { get; }
        public Uri LastUrl { get; }
        public IReadOnlyList<IDisposable> Disposables { get; }
        public Stream? Stream { get; }
        public DateTimeOffset LastModified { get; }
        public string ETag { get; }
        public ILookup<string, string> Headers { get; }

        public void Dispose()
        {
            Stream?.Dispose();
            foreach (var disposable in Disposables)
            {
                disposable?.Dispose();
            }
        }
    }

    public class ExternalBlobStorageClient
    {
        private readonly Func<HttpClient> _httpClientFactory;
        private readonly RedirectResolver _redirectResolver;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IThrottle _throttle;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<ExternalBlobStorageClient> _logger;

        public ExternalBlobStorageClient(
            Func<HttpClient> httpClientFactory,
            RedirectResolver redirectResolver,
            ServiceClientFactory serviceClientFactory,
            IThrottle throttle,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsSettings> options,
            ILogger<ExternalBlobStorageClient> logger)
        {
            _httpClientFactory = httpClientFactory;
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
            var latest = timestamps.MaxBy(x => x.Response.LastModified)!;
            if (DateTimeOffset.UtcNow - latest.Response.LastModified > latest.Endpoint.AgeLimit)
            {
                throw new InvalidOperationException(
                    $"The last modified {latest.Endpoint.Name} URL is {latest.Endpoint.Url} " +
                    $"(modified on {latest.Response.LastModified:O}) " +
                    $"but this is older than the age limit of {latest.Endpoint.AgeLimit}. " +
                    $"Check for stale data or bad configuration.");
            }

            return await DownloadAsync(latest.Response, latest.Endpoint.Deserialize);
        }

        public async Task<AsOfData<T>> DownloadNewestAsync<T>(IReadOnlyCollection<Uri> urls, TimeSpan ageLimit, string name, Func<Stream, IAsyncEnumerable<IReadOnlyList<T>>> deserialize)
        {
            var endpoints = urls
                .Select(x => new BlobStorageJsonEndpoint<T>(x, ageLimit, name, deserialize))
                .ToList();
            return await DownloadNewestAsync(endpoints, name);
        }

        private async Task<(BlobStorageJsonEndpoint<T> Endpoint, ExternalBlobResponse Response)> GetAsOfTimestampAsync<T>(BlobStorageJsonEndpoint<T> endpoint)
        {
            ExternalBlobResponse? response = null;
            try
            {
                response = await GetResponseAsync(new ExternalBlobRequest(
                    BlobRequestMethod.Head,
                    endpoint.Url,
                    UseThrottle: true,
                    AllowNotFound: false));

                var age = DateTimeOffset.UtcNow - response!.LastModified;
                _telemetryClient.TrackMetric(
                    nameof(ExternalBlobStorageClient) + "." + nameof(GetAsOfTimestampAsync) + ".AgeMinutes",
                    age.TotalMinutes,
                    new Dictionary<string, string>
                    {
                        { "InitialUrl", endpoint.Url.AbsoluteUri },
                        { "LastUrl", response.LastUrl.AbsoluteUri },
                        { "ClientType", response.ClientType.ToString() },
                    });

                return (endpoint, response);
            }
            finally
            {
                response?.Dispose();
                _throttle.Release();
            }
        }

        private async Task<AsOfData<T>> DownloadAsync<T>(ExternalBlobResponse latest, Func<Stream, IAsyncEnumerable<IReadOnlyList<T>>> deserialize)
        {
            var result = await GetResponseAsync(new ExternalBlobRequest(
                BlobRequestMethod.Get,
                latest.Request.Url,
                UseThrottle: true,
                AllowNotFound: false));

            return new AsOfData<T>(
                result!.LastModified,
                latest.Request.Url,
                result.ETag,
                AsyncEnumerableEx.Using(
                    () => latest.Request.UseThrottle ? new ThrottledDisposable(result, _throttle) : (IDisposable)result,
                    _ => deserialize(result.Stream!)));
        }

        public async Task<ExternalBlobResponse?> GetResponseAsync(ExternalBlobRequest request)
        {
            try
            {
                if (request.UseThrottle)
                {
                    await _throttle.WaitAsync();
                }

                var useBlobClient = _options.Value.UseBlobClientForExternalData;
                Uri? lastUrl = null;
                BlobClient? blobClient = null;

                if (useBlobClient != false)
                {
                    lastUrl = await _redirectResolver.FollowRedirectsAsync(request.Url);
                    blobClient = await _serviceClientFactory.TryGetBlobClientAsync(lastUrl);
                    if (useBlobClient == true && blobClient is null)
                    {
                        throw new InvalidOperationException("Unable to build a blob client for URL: " + lastUrl.Obfuscate());
                    }
                }

                if (blobClient is not null)
                {
                    return await GetResponseWithBlobClientAsync(request, lastUrl!, blobClient);
                }
                else
                {
                    return await GetResponseWithHttpClientAsync(request, lastUrl);
                }
            }
            catch
            {
                if (request.UseThrottle)
                {
                    _throttle.Release();
                }
                throw;
            }
        }

        private async Task<ExternalBlobResponse?> GetResponseWithBlobClientAsync(ExternalBlobRequest request, Uri lastUrl, BlobClient blobClient)
        {
            Response<BlobDownloadStreamingResult>? streamingResponse = null;
            Response? rawResponse = null;
            try
            {
                switch (request.Method)
                {
                    case BlobRequestMethod.Head:
                        var propertiesResponse = await blobClient.GetPropertiesAsync();
                        rawResponse = propertiesResponse.GetRawResponse();
                        return new ExternalBlobResponse(
                            request,
                            BlobClientType.BlobClient,
                            lastUrl,
                            propertiesResponse.Value.LastModified,
                            propertiesResponse.Value.ETag.ToString(),
                            GetHeaders(rawResponse.Headers));

                    case BlobRequestMethod.Get:
                        streamingResponse = await blobClient.DownloadStreamingAsync();
                        rawResponse = streamingResponse.GetRawResponse();
                        return new ExternalBlobResponse(
                            request,
                            BlobClientType.BlobClient,
                            lastUrl,
                            [rawResponse, streamingResponse.Value],
                            streamingResponse.Value.Content,
                            streamingResponse.Value.Details.LastModified,
                            streamingResponse.Value.Details.ETag.ToString(),
                            GetHeaders(rawResponse.Headers));

                    default:
                        throw new NotImplementedException();
                }
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                streamingResponse?.Value.Dispose();
                rawResponse?.Dispose();
                return null;
            }
            catch
            {
                streamingResponse?.Value.Dispose();
                rawResponse?.Dispose();
                throw;
            }
        }

        private static ILookup<string, string> GetHeaders(ResponseHeaders headers)
        {
            return headers.ToLookup(x => x.Name, x => x.Value);
        }

        private async Task<ExternalBlobResponse?> GetResponseWithHttpClientAsync(ExternalBlobRequest request, Uri? lastUrl)
        {
            HttpResponseMessage? response = null;
            try
            {
                var (httpMethod, httpCompletionOption) = request.Method switch
                {
                    BlobRequestMethod.Head => (HttpMethod.Head, HttpCompletionOption.ResponseContentRead),
                    BlobRequestMethod.Get => (HttpMethod.Get, HttpCompletionOption.ResponseHeadersRead),
                    _ => throw new NotImplementedException(),
                };
                var httpRequest = new HttpRequestMessage(httpMethod, lastUrl ?? request.Url);

                // Prior to this version, Azure Blob Storage did not put quotes around etag headers...
                httpRequest.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                var httpClient = _httpClientFactory();
                response = await httpClient.SendAsync(httpRequest, httpCompletionOption);

                if (request.AllowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                {
                    response.Dispose();
                    return null;
                }

                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.LastModified is null)
                {
                    throw new InvalidOperationException($"No Last-Modified header was returned for URL {request.Url.AbsoluteUri}.");
                }

                if (response.Headers.ETag is null)
                {
                    throw new InvalidOperationException($"No ETag header was returned for URL {request.Url.AbsoluteUri}.");
                }

                var asOfTimestamp = response.Content.Headers.LastModified.Value.ToUniversalTime();
                var etag = response.Headers.ETag.ToString();
                var stream = await response.Content.ReadAsStreamAsync();
                lastUrl = response.RequestMessage?.RequestUri ?? httpRequest.RequestUri!;

                return new ExternalBlobResponse(
                    request,
                    BlobClientType.HttpClient,
                    lastUrl,
                    [response],
                    stream,
                    asOfTimestamp,
                    etag,
                    response.GetHeaderLookup());
            }
            catch
            {
                response?.Dispose();
                throw;
            }
        }
    }
}
