using System;
using System.Globalization;
using System.IO;

namespace Knapcode.ExplorePackages
{
    internal static class CsvUtility
    {
        public static void WriteWithQuotes(TextWriter writer, string value)
        {
            if (value == null)
            {
                return;
            }

            if (value.StartsWith(" ")
                || value.EndsWith(" ")
                || value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) > -1)
            {
                writer.Write('"');
                writer.Write(value.Replace("\"", "\"\""));
                writer.Write('"');
            }
            else
            {
                writer.Write(value);
            }
        }

        public static T? ParseNullable<T>(string input, Func<string, T> parse) where T : struct
        {
            return input.Length > 0 ? parse(input) : new T?();
        }

        public static string FormatDateTimeOffset(DateTimeOffset input)
        {
            return input.ToString("O", CultureInfo.InvariantCulture);
        }

        public static string FormatDateTimeOffset(DateTimeOffset? input)
        {
            return input?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public static DateTimeOffset ParseDateTimeOffset(string input)
        {
            return DateTimeOffset.ParseExact(input, "O", CultureInfo.InvariantCulture);
        }
    }
}
