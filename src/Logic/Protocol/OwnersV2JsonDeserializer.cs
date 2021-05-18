// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class OwnersV2JsonDeserializer
    {
        public async IAsyncEnumerable<PackageOwner> DeserializeAsync(TextReader reader, Stack<IDisposable> disposables, IThrottle throttle)
        {
            try
            {
                using var jsonReader = new JsonTextReader(reader);

                if (!await jsonReader.ReadAsync() || jsonReader.TokenType != JsonToken.StartObject)
                {
                    throw new InvalidDataException("Expected a JSON document starting with an object.");
                }

                string id = null;
                while (await jsonReader.ReadAsync() && jsonReader.TokenType != JsonToken.EndObject)
                {
                    switch (jsonReader.TokenType)
                    {
                        case JsonToken.PropertyName:
                            id = (string)jsonReader.Value;
                            break;
                        case JsonToken.String:
                            var username = (string)jsonReader.Value;
                            yield return new PackageOwner(id, username);
                            break;
                    }
                }

                if (await jsonReader.ReadAsync())
                {
                    throw new InvalidDataException("Expected the JSON document to end with the end of an object.");
                }
            }
            finally
            {
                throttle.Release();

                while (disposables.Any())
                {
                    disposables.Pop()?.Dispose();
                }
            }
        }
    }
}
