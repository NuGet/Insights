// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    [Flags]
    public enum PackageAssemblyEdgeCases
    {
        None = 0,
        Name_CultureNotFoundException = 1 << 0,
        Name_FileLoadException = 1 << 1,
        PublicKeyToken_Security = 1 << 2,
        CustomAttributes_TruncatedAttributes = 1 << 3,
        CustomAttributes_TruncatedFailedDecode = 1 << 4,
        CustomAttributes_MethodDefinition = 1 << 5,
        CustomAttributes_TypeDefinitionConstructor = 1 << 6,
        CustomAttributes_DuplicateArgumentName = 1 << 7,
        CustomAttributes_BrokenMethodDefinitionName = 1 << 8,
        CustomAttributes_ArrayOutOfMemory = 1 << 9,
        CustomAttributes_BrokenValueBlob = 1 << 10,
        CustomAttributes_TypeSpecificationConstructor = 1 << 11,
    }
}
