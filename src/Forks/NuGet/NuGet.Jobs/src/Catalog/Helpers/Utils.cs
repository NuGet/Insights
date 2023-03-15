﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public static class Utils
    {
        private static readonly char[] TagTrimChars = { ',', ' ', '\t', '|', ';' };

        public static string[] SplitTags(string original)
        {
            var fields = original
                .Split(TagTrimChars)
                .Select(w => w.Trim(TagTrimChars))
                .Where(w => w.Length > 0)
                .ToArray();

            return fields;
        }
    }
}
