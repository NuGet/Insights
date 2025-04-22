// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.FileSystemHttpCache
{
    public record FileSystemHttpCacheKey(
        string Directory,
        string Key,
        string? KeyInfo,
        string? KeyInfoExtension,
        string RequestBodyExtension,
        string ResponseBodyExtension);
}
