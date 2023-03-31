// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace NuGet.Insights
{
    public static class NuGetInsightsMessagePack
    {
        public static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions
            .Standard
            .WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[]
                {
                },
                new IFormatterResolver[]
                {
                    CsvRecordFormatterResolver.Instance,
                    NuGetInsightsFormatterResolver.Instance,
                    StandardResolver.Instance
                }))
            .WithCompression(MessagePackCompression.Lz4Block);
    }
}
