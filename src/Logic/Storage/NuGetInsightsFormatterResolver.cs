// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using MessagePack;
using MessagePack.Formatters;

namespace NuGet.Insights
{
    public class NuGetInsightsFormatterResolver : IFormatterResolver
    {
        public static NuGetInsightsFormatterResolver Instance { get; } = new NuGetInsightsFormatterResolver();

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                Formatter = (IMessagePackFormatter<T>)GetFormatter(typeof(T));
            }

            private static object GetFormatter(Type outputType)
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
                return null;
            }
        }
    }
}
