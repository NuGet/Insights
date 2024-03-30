// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class PackageDownloadsV2Client
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new();

        private readonly BlobStorageJsonClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PackageDownloadsV2Client(BlobStorageJsonClient storageClient, IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<PackageDownloads>> GetAsync()
        {
            return await _storageClient.DownloadNewestAsync(
                _options.Value.DownloadsV2Urls,
                _options.Value.DownloadsV2AgeLimit,
                "downloads.v2.json",
                DeserializeAsync);
        }

        public static async IAsyncEnumerable<PackageDownloads> DeserializeAsync(Stream stream)
        {
            using var textReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(textReader);

            if (!await jsonReader.ReadAsync() || jsonReader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidDataException("Expected a JSON document starting with an object.");
            }

            while (jsonReader.TokenType == JsonToken.PropertyName)
            {
                var id = (string?)jsonReader.Value;

                jsonReader.Read();

                if (jsonReader.TokenType != JsonToken.StartObject)
                {
                    throw new InvalidOperationException("The token after the package ID should be the start of an object.");
                }

                jsonReader.Read();

                while (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    var version = (string)jsonReader.Value!;

                    jsonReader.Read();
                    if (jsonReader.TokenType != JsonToken.Integer)
                    {
                        throw new InvalidOperationException("The token after the package version should be an integer.");
                    }

                    var downloads = (long)jsonReader.Value!;

                    jsonReader.Read();

                    yield return new PackageDownloads(id, version, downloads);
                }

                if (jsonReader.TokenType == JsonToken.EndObject)
                {
                    throw new InvalidOperationException("The token after the package versions should be the end of an object.");
                }

                jsonReader.Read();
            }

            if (jsonReader.TokenType == JsonToken.EndObject)
            {
                throw new InvalidDataException("The last token should be the end of an object.");
            }

            if (await jsonReader.ReadAsync())
            {
                throw new InvalidDataException("Expected the JSON document to end with the end of an object.");
            }
        }
    }
}
