using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.Extensions.ObjectPool;

namespace Knapcode.ExplorePackages
{
    public class CsvRecordFormatterResolver : IFormatterResolver
    {
        internal static readonly DefaultObjectPool<List<string>> ListPool = new DefaultObjectPool<List<string>>(new StringListPolicy());

        private class StringListPolicy : PooledObjectPolicy<List<string>>
        {
            public override List<string> Create()
            {
                return new List<string>();
            }

            public override bool Return(List<string> obj)
            {
                obj.Clear();
                return true;
            }
        }

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

                if (csvRecordInterface == null)
                {
                    return null;
                }

                var formatterType = typeof(CsvRecordFormatter<>).MakeGenericType(outputType);
                return Activator.CreateInstance(formatterType);
            }
        }
    }
}
