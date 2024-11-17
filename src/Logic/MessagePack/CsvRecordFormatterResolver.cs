// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;

namespace NuGet.Insights
{
    public class CsvRecordFormatterResolver : IFormatterResolver
    {
        public static CsvRecordFormatterResolver Instance { get; } = new CsvRecordFormatterResolver();

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
                var csvRecordType = typeof(ICsvRecord);
                var csvRecordInterface = outputType
                    .GetInterfaces()
                    .Where(x => x == csvRecordType)
                    .FirstOrDefault();

                if (csvRecordType != outputType && csvRecordInterface == null)
                {
                    return null;
                }

                var formatterInterface = typeof(IMessagePackFormatter<>).MakeGenericType(outputType);

                // This nested formatter type is created by a source generator.
                var nestedFormatter = outputType
                    .GetNestedTypes(BindingFlags.Public)
                    .Where(x => x.IsAssignableTo(formatterInterface))
                    .Single();
                return Activator.CreateInstance(nestedFormatter);
            }
        }
    }
}
