// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;

namespace NuGet.Insights
{
    public static class ByteArrayExtensions
    {
        public static string ToUpperHex(this byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToUpperInvariant();
        }

        public static string ToLowerHex(this byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        public static string ToBase64(this byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        public static string ToTrimmedBase32(this byte[] bytes)
        {
            return Base32.ToBase32(bytes).TrimEnd('=').ToLowerInvariant();
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/321404
        /// </summary>
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable
                .Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
}
