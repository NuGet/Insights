// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.FileSystemHttpCache
{
    public record FileSystemHttpCacheSettings(
        string Directory,
        Predicate<HttpRequestMessage> IsApplicable,
        GenerateFileSystemHttpCacheKeyAsync GenerateKeyAsync,
        SanitizeFileSystemHttpCacheRequestStreamAsync SanitizeRequestStreamAsync,
        SanitizeFileSystemHttpCacheResponseStreamAsync SanitizeResponseStreamAsync);

    public delegate Task<FileSystemHttpCacheKey?> GenerateFileSystemHttpCacheKeyAsync(FileSystemHttpCacheSettings settings, HttpRequestMessage request, Stream requestBody);
    public delegate Task SanitizeFileSystemHttpCacheRequestStreamAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream, HttpRequestMessage request);
    public delegate Task SanitizeFileSystemHttpCacheResponseStreamAsync(FileSystemHttpCacheKey cacheKey, FileSystemHttpCacheEntryType type, Stream stream, HttpResponseMessage response);
}
