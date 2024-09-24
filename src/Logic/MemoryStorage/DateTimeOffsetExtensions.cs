// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Azure;

namespace NuGet.Insights.MemoryStorage
{
    public static class DateTimeOffsetExtensions
    {
        public static ETag ToMemoryETag(this DateTimeOffset lastModified, bool weak)
        {
            if (weak)
            {
                return new ETag($"W/\"{lastModified.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.FFFFFFF}Z\"");
            }
            else
            {
                return new ETag($"\"{lastModified.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.FFFFFFF}Z\"");
            }
        }
    }
}
