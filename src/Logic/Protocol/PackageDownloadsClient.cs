// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class PackageDownloadsClient
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            Converters = { new PackageIdDownloadsConverter() },
        };

        private readonly HttpClient _httpClient;
        private readonly IThrottle _throttle;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PackageDownloadsClient(
            HttpClient httpClient,
            IThrottle throttle,
            IOptions<NuGetInsightsSettings> options)
        {
            _httpClient = httpClient;
            _throttle = throttle;
            _options = options;
        }

        public async Task<AsOfData<PackageDownloads>> GetAsync()
        {
            if (_options.Value.DownloadsV1Url == null)
            {
                throw new InvalidOperationException("The downloads.v1.json URL is required.");
            }

            HttpResponseMessage response = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _options.Value.DownloadsV1Url);

                // Prior to this version, Azure Blob Storage did not put quotes around etag headers...
                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                await _throttle.WaitAsync();
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                var asOfTimestamp = response.Content.Headers.LastModified.Value.ToUniversalTime();
                var etag = response.Headers.ETag.ToString();
                var stream = await response.Content.ReadAsStreamAsync();

                return new AsOfData<PackageDownloads>(
                    asOfTimestamp,
                    _options.Value.DownloadsV1Url,
                    etag,
                    AsyncEnumerableEx.Using(
                        () => new ResponseAndThrottle(response, _throttle),
                        _ => Deserialize(stream)));
            }
            catch
            {
                response?.Dispose();
                _throttle.Release();
                throw;
            }
        }

        private static async IAsyncEnumerable<PackageDownloads> Deserialize(Stream stream)
        {
            var items = JsonSerializer.DeserializeAsyncEnumerable<PackageIdDownloads>(stream, JsonSerializerOptions);
            await foreach (var item in items)
            {
                foreach (var version in item.Versions)
                {
                    yield return new PackageDownloads(item.Id, version.Version, version.Downloads);
                }
            }
        }

        private class PackageIdDownloadsConverter : JsonConverter<PackageIdDownloads>
        {
            public override PackageIdDownloads Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                reader.AssertType(JsonTokenType.StartArray);
                reader.AssertReadAndType(JsonTokenType.String);
                var output = new PackageIdDownloads(reader.GetString());
                reader.AssertRead();
                while (reader.TokenType == JsonTokenType.StartArray)
                {
                    reader.AssertRead();
                    var version = reader.GetString();
                    reader.AssertRead();
                    var downloads = reader.GetInt64();
                    reader.AssertReadAndType(JsonTokenType.EndArray);
                    output.Versions.Add((version, downloads));
                    reader.AssertRead();
                }

                reader.AssertType(JsonTokenType.EndArray);

                return output;
            }

            public override void Write(Utf8JsonWriter writer, PackageIdDownloads value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        private class PackageIdDownloads
        {
            public PackageIdDownloads(string id)
            {
                Id = id;
                Versions = new List<(string Version, long Downloads)>();
            }

            public string Id { get; }
            public List<(string Version, long Downloads)> Versions { get; }
        }
    }
}
