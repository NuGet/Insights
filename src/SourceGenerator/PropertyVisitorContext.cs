// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class PropertyVisitorContext
    {
        public PropertyVisitorContext(
            GeneratorExecutionContext generatorExecutionContext,
            INamedTypeSymbol nullable,
            INamedTypeSymbol kustoTypeAttribute)
        {
            GeneratorExecutionContext = generatorExecutionContext;
            Nullable = nullable;
            KustoTypeAttribute = kustoTypeAttribute;
        }

        public GeneratorExecutionContext GeneratorExecutionContext { get; }
        public INamedTypeSymbol Nullable { get; }
        public INamedTypeSymbol KustoTypeAttribute { get; }
    }
}
