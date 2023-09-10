// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

#nullable enable

namespace NuGet.Insights
{
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

        public async Task<AsOfData<T>> DownloadNewestAsync<T>(IReadOnlyCollection<string> urls, TimeSpan ageLimit, string name, Func<Stream, IAsyncEnumerable<T>> deserialize)
        {
            if (urls is null || urls.Count == 0)
            {
                throw new InvalidOperationException($"The {name} URL is required.");
            }

            var timestampTasks = urls.Select(GetAsOfTimestampAsync).ToList();
            var timestamps = await Task.WhenAll(timestampTasks);
            var latest = timestamps.MaxBy(x => x.AsOfTimestamp)!;
            if (DateTimeOffset.UtcNow - latest.AsOfTimestamp > ageLimit)
            {
                throw new InvalidOperationException(
                    $"The last modified {name} URL is {latest.Url} " +
                    $"(modified on {latest.AsOfTimestamp:O}) " +
                    $"but this is older than the age limit of {ageLimit}. " +
                    $"Check for stale data or bad configuration.");
            }

            return await DownloadAsync(latest.Url, deserialize);
        }

        private async Task<(string Url, DateTimeOffset AsOfTimestamp)> GetAsOfTimestampAsync(string url)
        {
            HttpResponseMessage? response = null;
            try
            {
                DateTimeOffset asOfTimestamp;
                (response, asOfTimestamp, _) = await GetResponseAsync(HttpMethod.Head, url, HttpCompletionOption.ResponseContentRead);

                var age = DateTimeOffset.UtcNow - asOfTimestamp;

                _telemetryClient.TrackMetric(
                    nameof(BlobStorageJsonClient) + "." + nameof(GetAsOfTimestampAsync) + ".AgeMinutes",
                    age.TotalMinutes,
                    new Dictionary<string, string>
                    {
                        { "Url", url },
                    });

                return (url, asOfTimestamp);
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
