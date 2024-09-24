// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public static class UriExtensions
    {
        /// <summary>
        /// Source: https://github.com/Azure/azure-sdk-for-net/blob/879876eb24351c6a78ca2923d137f3ad8be3fb91/sdk/storage/Azure.Storage.Common/src/Shared/UriExtensions.cs#L24
        /// </summary>
        public static Uri AppendToPath(this Uri uri, string segment)
        {
            UriBuilder uriBuilder = new UriBuilder(uri);
            string path = uriBuilder.Path;
            string text = ((path.Length == 0 || path[path.Length - 1] != '/') ? "/" : "");
            segment = segment.Replace("%", "%25", StringComparison.Ordinal);
            uriBuilder.Path = uriBuilder.Path + text + segment;
            return uriBuilder.Uri;
        }
    }
}
