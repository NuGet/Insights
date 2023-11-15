// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.Insights
{
    public static class DateTimeOffsetExtensions
    {
        public static string ToZulu(this DateTimeOffset input)
        {
            return input.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture).Replace("+00:00", "Z", StringComparison.Ordinal);
        }
    }
}
