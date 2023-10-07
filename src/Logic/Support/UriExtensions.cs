// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

#nullable enable

namespace NuGet.Insights
{
    public static class UriExtensions
    {
        public static string? Obfuscate(this Uri? uri)
        {
            if (uri is null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(uri.Query))
            {
                return uri.AbsoluteUri;
            }

            var query = QueryHelpers.ParseQuery(uri.Query);
            var changed = false;

            if (query.ContainsKey("sig"))
            {
                query["sig"] = "REDACTED";
                changed = true;
            }

            return changed ? uri.GetLeftPart(UriPartial.Path) + new QueryBuilder(query).ToQueryString() : uri.AbsoluteUri;
        }
    }
}
