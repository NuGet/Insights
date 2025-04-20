// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public static class DirectoryHelper
    {
        public static string GetRepositoryRoot()
        {
            const string markerFile = "NuGet.config";
            var repoDir = Directory.GetCurrentDirectory();
            while (repoDir != null && !Directory.GetFiles(repoDir).Any(x => Path.GetFileName(x) == markerFile))
            {
                repoDir = Path.GetDirectoryName(repoDir);
            }

            if (repoDir == null)
            {
                throw new InvalidOperationException($"Unable to find the repository root. Current directory: {Directory.GetCurrentDirectory()}. Marker file: {markerFile}");
            }

            return repoDir;
        }
    }
}
