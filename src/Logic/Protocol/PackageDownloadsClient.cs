// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace NuGet.Insights
{
    public class PackageDownloadsClient
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            Converters = { new PackageIdDownloadsConverter() },
        };

        private readonly BlobStorageJsonClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PackageDownloadsClient(BlobStorageJsonClient storageClient, IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<PackageDownloads>> GetAsync()
        {
            var endpoints = new List<BlobStorageJsonEndpoint<PackageDownloads>>();

            if (_options.Value.DownloadsV1Urls != null)
            {
                endpoints.AddRange(_options.Value.DownloadsV1Urls.Select(url => new BlobStorageJsonEndpoint<PackageDownloads>(
                    url,
                    _options.Value.DownloadsV1AgeLimit,
                    "downloads.v1.json",
                    DeserializeV1Async)));
            }

            if (_options.Value.DownloadsV2Urls != null)
            {
                endpoints.AddRange(_options.Value.DownloadsV2Urls.Select(url => new BlobStorageJsonEndpoint<PackageDownloads>(
                    url,
                    _options.Value.DownloadsV2AgeLimit,
                    "downloads.v2.json",
                    DeserializeV2Async)));
            }

            return await _storageClient.DownloadNewestAsync(endpoints, "downloads.v1.json or downloads.v2.json");
        }

        public static async IAsyncEnumerable<PackageDownloads> DeserializeV1Async(Stream stream)
        {
            var items = JsonSerializer.DeserializeAsyncEnumerable<PackageIdDownloads>(stream, JsonSerializerOptions);
            await foreach (var item in items)
            {
                if (item is null)
                {
                    continue;
                }

                foreach (var version in item.Versions)
                {
                    yield return new PackageDownloads(item.Id, version.Version, version.Downloads);
                }
            }
        }

        public static async IAsyncEnumerable<PackageDownloads> DeserializeV2Async(Stream stream)
        {
            using var textReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(textReader);

            if (!await jsonReader.ReadAsync() || jsonReader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidDataException("Expected a JSON document starting with an object.");
            }

            await jsonReader.ReadAsync();

            while (jsonReader.TokenType == JsonToken.PropertyName)
            {
                var id = (string?)jsonReader.Value;

                await jsonReader.ReadAsync();

                if (jsonReader.TokenType != JsonToken.StartObject)
                {
                    throw new InvalidOperationException("The token after the package ID should be the start of an object.");
                }

                await jsonReader.ReadAsync();

                while (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    var version = (string)jsonReader.Value!;

                    await jsonReader.ReadAsync();

                    if (jsonReader.TokenType != JsonToken.Integer)
                    {
                        throw new InvalidOperationException("The token after the package version should be an integer.");
                    }

                    var downloads = (long)jsonReader.Value!;

                    await jsonReader.ReadAsync();

                    yield return new PackageDownloads(id, version, downloads);
                }

                if (jsonReader.TokenType != JsonToken.EndObject)
                {
                    throw new InvalidOperationException("The token after the package versions should be the end of an object.");
                }

                await jsonReader.ReadAsync();
            }

            if (jsonReader.TokenType != JsonToken.EndObject)
            {
                throw new InvalidDataException("The last token should be the end of an object.");
            }

            if (await jsonReader.ReadAsync())
            {
                throw new InvalidDataException("Expected the JSON document to end with the end of an object.");
            }
        }


        private class PackageIdDownloadsConverter : System.Text.Json.Serialization.JsonConverter<PackageIdDownloads>
        {
            public override PackageIdDownloads Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                reader.AssertType(JsonTokenType.StartArray);
                reader.AssertReadAndType(JsonTokenType.String);
                var output = new PackageIdDownloads(reader.GetString()!);
                reader.AssertRead();
                while (reader.TokenType == JsonTokenType.StartArray)
                {
                    reader.AssertReadAndType(JsonTokenType.String);
                    var version = reader.GetString()!;
                    reader.AssertReadAndType(JsonTokenType.Number);
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
