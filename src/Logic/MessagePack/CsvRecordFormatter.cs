// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;

namespace NuGet.Insights
{
    public class CsvRecordFormatter<T> : IMessagePackFormatter<T> where T : ICsvRecord<T>
    {
        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            var fields = CsvRecordFormatterResolver.ListPool.Get();
            try
            {
                if (T.FieldCount > fields.Capacity)
                {
                    fields.Capacity = T.FieldCount;
                }

                value.Write(fields);
                writer.WriteArrayHeader(T.FieldCount);
                foreach (var field in fields)
                {
                    writer.Write(field);
                }
            }
            finally
            {
                CsvRecordFormatterResolver.ListPool.Return(fields);
            }
        }

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return default;
            }

            options.Security.DepthStep(ref reader);

            int i;
            var count = reader.ReadArrayHeader();
            var fields = CsvRecordFormatterResolver.ListPool.Get();
            try
            {
                for (i = 0; i < count; i++)
                {
                    fields.Add(reader.ReadString());
                }

                reader.Depth--;

                i = 0;
                return T.ReadNew(() => fields[i++]);
            }
            finally
            {
                CsvRecordFormatterResolver.ListPool.Return(fields);
            }
        }
    }
}
