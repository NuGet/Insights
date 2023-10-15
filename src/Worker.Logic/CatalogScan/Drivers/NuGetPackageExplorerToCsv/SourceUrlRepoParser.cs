// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public static class SourceUrlRepoParser
    {
        public static IReadOnlyList<SourceUrlRepoInfo> GetSourceRepoInfo(IEnumerable<string> sourceUrls)
        {
            var output = new Dictionary<SourceUrlRepo, SourceUrlRepoInfo>();
            foreach (var url in sourceUrls)
            {
                SourceUrlRepo sourceRepo;
                if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
                {
                    sourceRepo = new InvalidSourceRepo();
                }
                else if (parsedUrl.Host == "raw.githubusercontent.com")
                {
                    var pathPieces = parsedUrl.AbsolutePath.Split(new[] { '/' }, 5);
                    if (pathPieces.Length < 5 || pathPieces[0].Length > 0)
                    {
                        sourceRepo = new InvalidSourceRepo();
                    }
                    else
                    {
                        sourceRepo = new GitHubSourceRepo
                        {
                            Owner = pathPieces[1],
                            Repo = pathPieces[2],
                            Ref = pathPieces[3],
                        };
                    }
                }
                else
                {
                    sourceRepo = new UnknownSourceRepo
                    {
                        Host = parsedUrl.Host,
                    };
                }

                if (!output.TryGetValue(sourceRepo, out var info))
                {
                    info = new SourceUrlRepoInfo
                    {
                        Repo = sourceRepo,
                        Example = url,
                        FileCount = 0
                    };
                    output.Add(sourceRepo, info);
                }

                info.FileCount++;
            }

            return output
                .OrderByDescending(x => x.Value.FileCount)
                .ThenBy(x => x.Value.Example, StringComparer.Ordinal)
                .Select(x => x.Value)
                .ToList();
        }
    }
}
