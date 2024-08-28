// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageAssetToCsv
{
    public enum PatternSetType
    {
        RuntimeAssemblies = 1,
        CompileRefAssemblies,
        CompileLibAssemblies,
        NativeLibraries,
        ResourceAssemblies,
        MSBuildFiles,
        MSBuildMultiTargetingFiles,
        ContentFiles,
        ToolsAssemblies,
        EmbedAssemblies,
        MSBuildTransitiveFiles,
    }
}
