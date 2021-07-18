// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{
    public static class NuGetGallery
    {
        /// <summary>
        /// Source: https://github.com/NuGet/NuGetGallery/blob/7557469186f07c1a15fff57e5efd3816e622a776/src/NuGetGallery.Services/PackageManagement/PackageService.cs#L720-L818
        /// </summary>
        public static IEnumerable<NuGetFramework> GetSupportedFrameworks(NuspecReader nuspecReader, IReadOnlyList<string> packageFiles)
        {
            var supportedTFMs = Enumerable.Empty<NuGetFramework>();
            if (packageFiles != null && packageFiles.Any() && nuspecReader != null)
            {
                // Setup content items for analysis
                var items = new ContentItemCollection();
                items.Load(packageFiles);
                var runtimeGraph = new RuntimeGraph();
                var conventions = new ManagedCodeConventions(runtimeGraph);

                // Let's test for tools packages first--they're a special case
                var groups = Enumerable.Empty<ContentItemGroup>();
                var packageTypes = nuspecReader.GetPackageTypes();
                if (packageTypes.Count == 1 && (packageTypes[0] == PackageType.DotnetTool ||
                                                packageTypes[0] == PackageType.DotnetCliTool))
                {
                    // Only a package that is a tool package (and nothing else) will be matched against tools pattern set
                    groups = items.FindItemGroups(conventions.Patterns.ToolsAssemblies);
                }
                else
                {
                    // Gather together a list of pattern sets indicating the kinds of packages we wish to evaluate
                    var patterns = new[]
                    {
                        conventions.Patterns.CompileRefAssemblies,
                        conventions.Patterns.CompileLibAssemblies,
                        conventions.Patterns.RuntimeAssemblies,
                        conventions.Patterns.ContentFiles,
                        conventions.Patterns.ResourceAssemblies,
                    };

                    // Add MSBuild to this list, but we need to ensure we have package assets before they make the cut.
                    // A series of files in the right places won't matter if there's no {id}.props|targets.
                    var msbuildPatterns = new[]
                    {
                        conventions.Patterns.MSBuildFiles,
                        conventions.Patterns.MSBuildMultiTargetingFiles,
                    };

                    // We'll create a set of "groups" --these are content items which satisfy file pattern sets
                    var standardGroups = patterns
                        .SelectMany(p => items.FindItemGroups(p));

                    // Filter out MSBuild assets that don't match the package ID and append to groups we already have
                    var packageId = nuspecReader.GetId();
                    var msbuildGroups = msbuildPatterns
                        .SelectMany(p => items.FindItemGroups(p))
                        .Where(g => HasBuildItemsForPackageId(g.Items, packageId));
                    groups = standardGroups.Concat(msbuildGroups);
                }

                // Now that we have a collection of groups which have made it through the pattern set filter, let's transform them into TFMs
                supportedTFMs = groups
                    .SelectMany(p => p.Properties)
                    .Where(pair => pair.Key == ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker)
                    .Select(pair => pair.Value)
                    .Cast<NuGetFramework>()
                    .Distinct();
            }

            return supportedTFMs;
        }

        private static bool HasBuildItemsForPackageId(IEnumerable<ContentItem> items, string packageId)
        {
            foreach (var item in items)
            {
                var fileName = Path.GetFileName(item.Path);
                if (fileName == PackagingCoreConstants.EmptyFolder)
                {
                    return true;
                }

                if ($"{packageId}.props".Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if ($"{packageId}.targets".Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
