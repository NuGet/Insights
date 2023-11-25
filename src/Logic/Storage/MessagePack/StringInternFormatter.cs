// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;

#nullable enable

namespace NuGet.Insights
{
    public class StringInternFormatter : IMessagePackFormatter<string?>
    {
        public string? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            var internPool = (options as NuGetInsightsMessagePackSerializerOptions)?.InternPool;
            if (internPool is null)
            {
                return reader.ReadString();
            }

            MessagePackReader retryReader = reader;
            if (reader.TryReadStringSpan(out ReadOnlySpan<byte> bytes))
            {
                if (bytes.Length < 4096)
                {
                    if (bytes.Length == 0)
                    {
                        return string.Empty;
                    }

                    return internPool.InternUtf8(bytes);
                }
                else
                {
                    // Rewind the reader to the start of the string because we're taking the slow path.
                    reader = retryReader;
                }
            }

            return internPool.Intern(reader.ReadString());
        }

        public void Serialize(ref MessagePackWriter writer, string? value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }
    }
}
