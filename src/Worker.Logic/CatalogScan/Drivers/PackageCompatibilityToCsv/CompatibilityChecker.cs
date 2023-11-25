// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Commands;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.Protocol.Core.Types;
using INuGetLogger = NuGet.Common.ILogger;
using NuGetNullLogger = NuGet.Common.NullLogger;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{
    public static class CompatibilityChecker
    {
        private static readonly Assembly NuGetCommands = typeof(RestoreCommand).Assembly;

        private static readonly Type CompatibilityDataType = NuGetCommands.GetType("NuGet.Commands.CompatibilityChecker+CompatibilityData");
        private static readonly ConstructorInfo CompatibilityDataConstructor = CompatibilityDataType
            .GetConstructors()
            .Single();

        private static readonly Type CompatibilityCheckerType = NuGetCommands.GetType("NuGet.Commands.CompatibilityChecker");
        private static readonly MethodInfo GetPackageFrameworksMethod = CompatibilityCheckerType
            .GetMethod("GetPackageFrameworks", BindingFlags.Static | BindingFlags.NonPublic);

        public static IEnumerable<NuGetFramework> GetPackageFrameworks(IEnumerable<string> files, INuGetLogger logger)
        {
            var compatibilityData = CompatibilityDataConstructor.Invoke(new object[] { files, null, null });

            var restoreTargetGraph = RestoreTargetGraph.Create(
                new List<GraphNode<RemoteResolveResult>>(),
                new RemoteWalkContext(
                    new SourceCacheContext(),
                    PackageSourceMapping.GetPackageSourceMapping(NullSettings.Instance),
                    NuGetNullLogger.Instance),
                logger,
                NuGetFramework.AnyFramework);

            return (IEnumerable<NuGetFramework>)GetPackageFrameworksMethod
                .Invoke(null, new object[] { compatibilityData, restoreTargetGraph });
        }
    }
}
