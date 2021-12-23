// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class VerifiedPackagesV1JsonDeserializer
    {
        public async IAsyncEnumerable<VerifiedPackage> DeserializeAsync(TextReader reader, Stack<IDisposable> disposables, IThrottle throttle)
        {
            try
            {
                using var jsonReader = new JsonTextReader(reader);

                if (!await jsonReader.ReadAsync() || jsonReader.TokenType != JsonToken.StartArray)
                {
                    throw new InvalidDataException("Expected a JSON document starting with an array.");
                }

                while (await jsonReader.ReadAsync() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    switch (jsonReader.TokenType)
                    {
                        case JsonToken.String:
                            var id = (string)jsonReader.Value;
                            yield return new VerifiedPackage(id);
                            break;
                    }
                }

                if (await jsonReader.ReadAsync())
                {
                    throw new InvalidDataException("Expected the JSON document to end with the end of an array.");
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
