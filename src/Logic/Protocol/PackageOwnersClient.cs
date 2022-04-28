// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

#nullable enable

namespace NuGet.Insights
{
    public class PackageOwnersClient
    {
        private readonly BlobStorageJsonClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PackageOwnersClient(
            BlobStorageJsonClient storageClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<PackageOwner>> GetAsync()
        {
            if (_options.Value.OwnersV2Url == null)
            {
                throw new InvalidOperationException("The owners.v2.json URL is required.");
            }

            return await _storageClient.DownloadAsync(_options.Value.OwnersV2Url, DeserializeAsync);
        }

        private async IAsyncEnumerable<PackageOwner> DeserializeAsync(Stream stream)
        {
            using var textReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(textReader);

            if (!await jsonReader.ReadAsync() || jsonReader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidDataException("Expected a JSON document starting with an object.");
            }

            string? id = null;
            while (await jsonReader.ReadAsync() && jsonReader.TokenType != JsonToken.EndObject)
            {
                switch (jsonReader.TokenType)
                {
                    case JsonToken.PropertyName:
                        id = (string)jsonReader.Value!;
                        break;
                    case JsonToken.String:
                        var username = (string)jsonReader.Value!;
                        yield return new PackageOwner(id, username);
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
