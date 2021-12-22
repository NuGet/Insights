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
    public class VerifiedPackagesClient
    {
        private readonly HttpClient _httpClient;
        private readonly IThrottle _throttle;
        private readonly VerifiedPackagesV1JsonDeserializer _deserializer;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public VerifiedPackagesClient(
            HttpClient httpClient,
            IThrottle throttle,
            VerifiedPackagesV1JsonDeserializer deserializer,
            IOptions<NuGetInsightsSettings> options)
        {
            _httpClient = httpClient;
            _throttle = throttle;
            _deserializer = deserializer;
            _options = options;
        }

        public async Task<AsOfData<VerifiedPackage>> GetAsync()
        {
            if (_options.Value.VerifiedPackagesV1Url == null)
            {
                throw new InvalidOperationException("The verifiedPackages.json URL is required.");
            }

            var disposables = new Stack<IDisposable>();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _options.Value.VerifiedPackagesV1Url);
                disposables.Push(request);

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

                return new AsOfData<VerifiedPackage>(
                    asOfTimestamp,
                    _options.Value.VerifiedPackagesV1Url,
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
