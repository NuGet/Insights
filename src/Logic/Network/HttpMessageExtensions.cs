// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public static class HttpMessageExtensions
    {
        public static ILookup<string, string> GetHeaderLookup(this HttpResponseMessage response)
        {
            return Enumerable.Empty<KeyValuePair<string, HeaderStringValues>>()
                .Concat(response.Headers.NonValidated)
                .Concat(response.Content.Headers.NonValidated)
                .SelectMany(x => x.Value.Select(y => new { x.Key, Value = y }))
                .ToLookup(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
