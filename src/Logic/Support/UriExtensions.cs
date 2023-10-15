// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

#nullable enable

namespace NuGet.Insights
{
    public static class UriExtensions
    {
        private static IReadOnlyList<string> SasQueryParameters = new[]
        {
            "epk",
            "erk",
            "rscc",
            "rscd",
            "rsce",
            "rscl",
            "rsct",
            "saoid",
            "scid",
            "sdd",
            "sdd",
            "se",
            "ses",
            "ses",
            "si",
            "sig",
            "sip",
            "ske",
            "skoid",
            "sks",
            "skt",
            "sktid",
            "skv",
            "sp",
            "spk",
            "spr",
            "sr",
            "srk",
            "srt",
            "ss",
            "st",
            "suoid",
            "sv",
            "sv",
            "tn",
        };

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
                foreach (var parameter in SasQueryParameters)
                {
                    query.Remove(parameter);
                }

                query["sig"] = "REDACTED";
                changed = true;
            }

            return changed ? uri.GetLeftPart(UriPartial.Path) + new QueryBuilder(query).ToQueryString() : uri.AbsoluteUri;
        }
    }
}
