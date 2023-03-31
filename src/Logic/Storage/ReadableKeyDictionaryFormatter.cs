// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace NuGet.Insights
{
    public class ReadableKeyDictionaryFormatter<TDictionary, TValue> : IMessagePackFormatter<TDictionary>
        where TDictionary : IDictionary<string, ReadableKey<TValue>>
    {
        public void Serialize(ref MessagePackWriter writer, TDictionary value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            var resolver = options.Resolver;
            var keyFormatter = resolver.GetFormatterWithVerify<string>();
            var valueFormatter = resolver.GetFormatterWithVerify<TValue>();

            writer.WriteMapHeader(value.Count);
            using var enumerator = value.GetEnumerator();

            while (enumerator.MoveNext())
            {
                writer.CancellationToken.ThrowIfCancellationRequested();

                var current = enumerator.Current;
                keyFormatter.Serialize(ref writer, current.Value.Key, options);
                valueFormatter.Serialize(ref writer, current.Value.Value, options);
            }
        }

        public TDictionary Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return default;
            }

            var resolver = options.Resolver;
            var keyFormatter = resolver.GetFormatterWithVerify<string>();
            var valueFormatter = resolver.GetFormatterWithVerify<TValue>();

            var count = reader.ReadMapHeader();
            var output = Activator.CreateInstance<TDictionary>();

            options.Security.DepthStep(ref reader);
            try
            {
                for (var i = 0; i < count; i++)
                {
                    reader.CancellationToken.ThrowIfCancellationRequested();
                    var key = keyFormatter.Deserialize(ref reader, options);
                    var value = valueFormatter.Deserialize(ref reader, options);

                    output.Add(key, new ReadableKey<TValue>(key, value));
                }
            }
            finally
            {
                reader.Depth--;
            }

            return output;
        }
    }
}
