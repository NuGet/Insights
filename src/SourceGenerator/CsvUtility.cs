// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    internal static class CsvUtility
    {
        private const string DateTimeOffsetUtcFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ";
        private const string DateTimeOffsetFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz";

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

        public static async Task WriteWithQuotesAsync(TextWriter writer, string value)
        {
            if (value == null)
            {
                return;
            }

            if (value.StartsWith(" ")
                || value.EndsWith(" ")
                || value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) > -1)
            {
                await writer.WriteAsync('"');
                await writer.WriteAsync(value.Replace("\"", "\"\""));
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
            if (input.EndsWith("Z"))
            {
                return DateTimeOffset.ParseExact(input, DateTimeOffsetUtcFormat, CultureInfo.InvariantCulture);
            }

            return DateTimeOffset.ParseExact(input, DateTimeOffsetFormat, CultureInfo.InvariantCulture);
        }
    }
}
