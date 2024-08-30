// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NuGet.Insights
{
    public class PropertyVisitorContext
    {
        public PropertyVisitorContext(
            GeneratorExecutionContext generatorExecutionContext,
            SyntaxTree syntaxTree,
            TypeDeclarationSyntax typeDeclarationSyntax,
            bool hasNoDDLAttribute,
            INamedTypeSymbol nullable,
            INamedTypeSymbol kustoTypeAttribute,
            INamedTypeSymbol requiredAttribute)
        {
            GeneratorExecutionContext = generatorExecutionContext;
            SyntaxTree = syntaxTree;
            TypeDeclarationSyntax = typeDeclarationSyntax;
            HasNoDDLAttribute = hasNoDDLAttribute;
            Nullable = nullable;
            KustoTypeAttribute = kustoTypeAttribute;
            RequiredAttribute = requiredAttribute;
        }

        public GeneratorExecutionContext GeneratorExecutionContext { get; }
        public SyntaxTree SyntaxTree { get; }
        public TypeDeclarationSyntax TypeDeclarationSyntax { get; }
        public bool HasNoDDLAttribute { get; }
        public INamedTypeSymbol Nullable { get; }
        public INamedTypeSymbol KustoTypeAttribute { get; }
        public INamedTypeSymbol RequiredAttribute { get; }
    }
}
