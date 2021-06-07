// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageAssetToCsv
{
    public enum PatternSetType
    {
        CompileLibAssemblies,
        CompileRefAssemblies,
        ContentFiles,
        EmbedAssemblies,
        MSBuildFiles,
        MSBuildMultiTargetingFiles,
        MSBuildTransitiveFiles,
        NativeLibraries,
        ResourceAssemblies,
        RuntimeAssemblies,
        ToolsAssemblies,
    }
}
