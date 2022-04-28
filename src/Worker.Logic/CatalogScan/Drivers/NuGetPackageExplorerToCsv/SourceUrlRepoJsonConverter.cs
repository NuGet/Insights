// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public class SourceUrlRepoJsonConverter : JsonConverter<SourceUrlRepo>
    {
        public override SourceUrlRepo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, SourceUrlRepo value, JsonSerializerOptions options)
        {
            // Each non-abstract class descending from KeyedDocument should be in this switch to allow serialization.
            switch (value)
            {
                case GitHubSourceRepo gitHub:
                    JsonSerializer.Serialize(writer, gitHub, options);
                    break;
                case InvalidSourceRepo invalid:
                    JsonSerializer.Serialize(writer, invalid, options);
                    break;
                case UnknownSourceRepo unknown:
                    JsonSerializer.Serialize(writer, unknown, options);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
