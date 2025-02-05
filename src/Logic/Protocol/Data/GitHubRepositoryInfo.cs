// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    /// <summary>
    /// Mimics https://github.com/NuGet/NuGetGallery/blob/main/src/NuGetGallery.Core/GitHub/RepositoryInformation.cs
    /// </summary>
    public class GitHubRepositoryInfo
    {
        [JsonConstructor]
        public GitHubRepositoryInfo(string url, int stars, string id, string description, IReadOnlyList<string> dependencies)
        {
            Url = url;
            Stars = stars;
            Id = id;
            Description = description;
            Dependencies = dependencies;
        }

        public string Url { get; }
        public int Stars { get; }
        public string Id { get; }
        public string Description { get; }
        public IReadOnlyList<string> Dependencies { get; }
    }
}
