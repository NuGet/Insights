using System;
using MessagePack;
using MessagePack.Formatters;

namespace NuGet.Insights
{
    public class CsvRecordFormatter<T> : IMessagePackFormatter<T> where T : ICsvRecord
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
                if (value.FieldCount > fields.Capacity)
                {
                    fields.Capacity = value.FieldCount;
                }

                value.Write(fields);
                writer.WriteArrayHeader(value.FieldCount);
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

        private static readonly T Factory = Activator.CreateInstance<T>();

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
                return (T)Factory.ReadNew(() => fields[i++]);
            }
            finally
            {
                CsvRecordFormatterResolver.ListPool.Return(fields);
            }
        }
    }
}
