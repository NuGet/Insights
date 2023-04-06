// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Ben.Collections.Specialized;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace NuGet.Insights
{
    public static class NuGetInsightsMessagePack
    {
        public static MessagePackSerializerOptions Options { get; private set; } = GetOptions(withStringIntern: false);
        public static MessagePackSerializerOptions OptionsWithStringIntern => GetOptions(withStringIntern: true);

        private static MessagePackSerializerOptions GetOptions(bool withStringIntern)
        {
            var options = MessagePackSerializerOptions
                .Standard
                .WithResolver(CompositeResolver.Create(
                    new IMessagePackFormatter[]
                    {
                    },
                    new IFormatterResolver[]
                    {
                        CsvRecordFormatterResolver.Instance,
                        withStringIntern ? NuGetInsightsFormatterResolver.WithStringIntern : NuGetInsightsFormatterResolver.WithoutStringIntern,
                        StandardResolver.Instance
                    }))
                .WithCompression(MessagePackCompression.Lz4Block);

            if (withStringIntern)
            {
                options = new NuGetInsightsMessagePackSerializerOptions(options) { InternPool = new InternPool() };
            }

            return options;
        }
    }
}
