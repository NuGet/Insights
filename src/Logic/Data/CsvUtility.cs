// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static class CsvUtility
    {
        private const string DateTimeOffsetUtcFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ";
        private const string DateTimeOffsetFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz";

        private const string NoNullString = "A null string cannot be written to CSV. It will round trip as an empty string. Set the property to an empty string instead.";

        private static readonly char[] QuotableChars = [',', '"', '\r', '\n'];

        public static void WriteWithQuotes(TextWriter writer, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), NoNullString);
            }

            if (value.StartsWith(" ", StringComparison.Ordinal)
                || value.EndsWith(" ", StringComparison.Ordinal)
                || value.IndexOfAny(QuotableChars) > -1)
            {
                writer.Write('"');
                writer.Write(value.Replace("\"", "\"\"", StringComparison.Ordinal));
                writer.Write('"');
            }
            else
            {
                writer.Write(value);
            }
        }

        public static async Task WriteWithQuotesAsync(TextWriter writer, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), NoNullString);
            }

            if (value.StartsWith(" ", StringComparison.Ordinal)
                || value.EndsWith(" ", StringComparison.Ordinal)
                || value.IndexOfAny(QuotableChars) > -1)
            {
                await writer.WriteAsync('"');
                await writer.WriteAsync(value.Replace("\"", "\"\"", StringComparison.Ordinal));
                await writer.WriteAsync('"');
            }
            else
            {
                await writer.WriteAsync(value);
            }
        }

        public static T ParseReference<T>(string input, Func<string, T> parse) where T : class
        {
            return string.IsNullOrEmpty(input) ? null : parse(input);
        }

        public static T? ParseNullable<T>(string input, Func<string, T> parse) where T : struct
        {
            return string.IsNullOrEmpty(input) ? new T?() : parse(input);
        }

        public static string FormatBool(bool input)
        {
            return input ? "true" : "false";
        }

        public static string FormatBool(bool? input)
        {
            return input.HasValue ? FormatBool(input.Value) : string.Empty;
        }

        public static string FormatDateTimeOffset(DateTimeOffset input)
        {
            if (input.Offset == TimeSpan.Zero)
            {
                return input.ToString(DateTimeOffsetUtcFormat, CultureInfo.InvariantCulture);
            }

            return input.ToString(DateTimeOffsetFormat, CultureInfo.InvariantCulture);
        }

        public static string FormatDateTimeOffset(DateTimeOffset? input)
        {
            return input.HasValue ? FormatDateTimeOffset(input.Value) : string.Empty;
        }

        public static DateTimeOffset ParseDateTimeOffset(string input)
        {
            if (input.EndsWith("Z", StringComparison.Ordinal))
            {
                return DateTimeOffset.ParseExact(input, DateTimeOffsetUtcFormat, CultureInfo.InvariantCulture);
            }

            return DateTimeOffset.ParseExact(input, DateTimeOffsetFormat, CultureInfo.InvariantCulture);
        }
    }
}
