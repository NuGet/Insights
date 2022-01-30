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

        public BlobStorageJsonClient(HttpClient httpClient, IThrottle throttle)
        {
            _httpClient = httpClient;
            _throttle = throttle;
        }

        public async Task<AsOfData<T>> DownloadAsync<T>(string url, Func<Stream, IAsyncEnumerable<T>> deserialize)
        {
            HttpResponseMessage? response = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Prior to this version, Azure Blob Storage did not put quotes around etag headers...
                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                await _throttle.WaitAsync();
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

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
    }
}
