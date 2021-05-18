// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights
{
    public static class ByteArrayExtensions
    {
        public static string ToHex(this byte[] bytes)
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
    }
}
