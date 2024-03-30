// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

#nullable enable

namespace NuGet.Insights
{
    public class PopularityTransfersClient
    {
        private readonly BlobStorageJsonClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PopularityTransfersClient(
            BlobStorageJsonClient storageClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<PopularityTransfer>> GetAsync()
        {
            return await _storageClient.DownloadNewestAsync(
                _options.Value.PopularityTransfersV1Urls,
                _options.Value.PopularityTransfersV1AgeLimit,
                "popularity-transfers.v1.json",
                DeserializeAsync);
        }

        private async IAsyncEnumerable<PopularityTransfer> DeserializeAsync(Stream stream)
        {
            using var textReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(textReader);

            if (!await jsonReader.ReadAsync() || jsonReader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidDataException("Expected a JSON document starting with an object.");
            }

            string? fromId = null;
            while (await jsonReader.ReadAsync() && jsonReader.TokenType != JsonToken.EndObject)
            {
                switch (jsonReader.TokenType)
                {
                    case JsonToken.PropertyName:
                        fromId = (string)jsonReader.Value!;
                        break;
                    case JsonToken.String:
                        var toId = (string)jsonReader.Value!;
                        yield return new PopularityTransfer(fromId, toId);
                        break;
                }
            }

            if (await jsonReader.ReadAsync())
            {
                throw new InvalidDataException("Expected the JSON document to end with the end of an object.");
            }
        }
    }
}
