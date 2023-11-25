// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;

namespace NuGet.Insights
{
    public class NuGetInsightsFormatterResolver : IFormatterResolver
    {
        private readonly bool _withStringIntern;

        public static NuGetInsightsFormatterResolver WithStringIntern { get; } = new NuGetInsightsFormatterResolver(withStringIntern: true);
        public static NuGetInsightsFormatterResolver WithoutStringIntern { get; } = new NuGetInsightsFormatterResolver(withStringIntern: false);

        private NuGetInsightsFormatterResolver(bool withStringIntern)
        {
            _withStringIntern = withStringIntern;
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return _withStringIntern ? FormatterCache<T>.FormatterWithStringIntern : FormatterCache<T>.FormatterWithoutStringIntern;
        }

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> FormatterWithStringIntern;
            public static readonly IMessagePackFormatter<T> FormatterWithoutStringIntern;

            static FormatterCache()
            {
                FormatterWithStringIntern = (IMessagePackFormatter<T>)GetFormatter(typeof(T), withStringIntern: true);
                FormatterWithoutStringIntern = (IMessagePackFormatter<T>)GetFormatter(typeof(T), withStringIntern: false);
            }

            private static object GetFormatter(Type outputType, bool withStringIntern)
            {
                if (outputType.IsGenericType
                    && outputType.GenericTypeArguments.Length == 1
                    && outputType.GenericTypeArguments[0].IsGenericType
                    && outputType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(ReadableKey<>)
                    && (outputType.GetGenericTypeDefinition() == typeof(CaseInsensitiveDictionary<>)
                        || outputType.GetGenericTypeDefinition() == typeof(CaseInsensitiveSortedDictionary<>)))
                {
                    var formatterType = typeof(ReadableKeyDictionaryFormatter<,>).MakeGenericType(
                        outputType,
                        outputType.GenericTypeArguments[0].GenericTypeArguments[0]);
                    return Activator.CreateInstance(formatterType);
                }

                if (withStringIntern && outputType == typeof(string))
                {
                    return new StringInternFormatter();
                }

                return null;
            }
        }
    }
}
