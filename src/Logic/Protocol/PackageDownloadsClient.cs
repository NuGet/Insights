// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class PackageDownloadsClient
    {
        private readonly HttpClient _httpClient;
        private readonly IThrottle _throttle;
        private readonly DownloadsV1JsonDeserializer _deserializer;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PackageDownloadsClient(
            HttpClient httpClient,
            IThrottle throttle,
            DownloadsV1JsonDeserializer deserializer,
            IOptions<NuGetInsightsSettings> options)
        {
            _httpClient = httpClient;
            _throttle = throttle;
            _deserializer = deserializer;
            _options = options;
        }

        public async Task<AsOfData<PackageDownloads>> GetAsync()
        {
            if (_options.Value.DownloadsV1Url == null)
            {
                throw new InvalidOperationException("The downloads.v1.json URL is required.");
            }

            var disposables = new Stack<IDisposable>();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _options.Value.DownloadsV1Url);
                disposables.Push(request);

                // Prior to this version, Azure Blob Storage did not put quotes around etag headers...
                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                await _throttle.WaitAsync();
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                disposables.Push(response);

                response.EnsureSuccessStatusCode();

                var asOfTimestamp = response.Content.Headers.LastModified.Value.ToUniversalTime();
                var etag = response.Headers.ETag.ToString();

                var stream = await response.Content.ReadAsStreamAsync();
                disposables.Push(stream);

                var textReader = new StreamReader(stream);
                disposables.Push(textReader);

                return new AsOfData<PackageDownloads>(
                    asOfTimestamp,
                    _options.Value.DownloadsV1Url,
                    etag,
                    _deserializer.DeserializeAsync(textReader, disposables, _throttle));
            }
            catch
            {
                _throttle.Release();

                while (disposables.Any())
                {
                    disposables.Pop()?.Dispose();
                }

                throw;
            }
        }
    }
}
