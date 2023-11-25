// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

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
            return await _storageClient.DownloadNewestAsync(
                _options.Value.DownloadsV1Urls,
                _options.Value.DownloadsV1AgeLimit,
                "downloads.v1.json",
                DeserializeAsync);
        }

        public static async IAsyncEnumerable<PackageDownloads> DeserializeAsync(Stream stream)
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

        private class PackageIdDownloadsConverter : JsonConverter<PackageIdDownloads>
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
