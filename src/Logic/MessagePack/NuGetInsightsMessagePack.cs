// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace NuGet.Insights
{
    public static class NuGetInsightsMessagePack
    {
        public static MessagePackSerializerOptions Options { get; private set; } = GetOptions();

        private static MessagePackSerializerOptions GetOptions()
        {
            var options = MessagePackSerializerOptions
                .Standard
                .WithResolver(CompositeResolver.Create(
                    [
                        new StringInterningFormatter(),
                    ],
                    [
                        CsvRecordFormatterResolver.Instance,
                        NuGetInsightsFormatterResolver.Instance,
                        StandardResolver.Instance
                    ]))
                .WithCompression(MessagePackCompression.Lz4Block);

            return options;
        }
    }
}
