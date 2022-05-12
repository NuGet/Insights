// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public static class PropertyHelper
    {
        public static string GetPrettyType(string classNamespacePrefix, ISet<string> propertyNames, IPropertySymbol symbol)
        {
            var propType = symbol.Type.ToString();
            var nonNullPropType = propType.TrimEnd('?');
            var prettyPropType = nonNullPropType;

            // Clean up the type name by removing unnecessary namespaces.
            const string systemPrefix = "System.";
            if (prettyPropType.StartsWith(systemPrefix) && prettyPropType.IndexOf('.', systemPrefix.Length) < 0)
            {
                prettyPropType = prettyPropType.Substring(systemPrefix.Length);
            }
            else if (prettyPropType.StartsWith(classNamespacePrefix))
            {
                prettyPropType = prettyPropType.Substring(classNamespacePrefix.Length);
            }

            // To avoid property name collisions, only use the pretty version if it doesn't match a property name.
            if (propertyNames.Contains(prettyPropType))
            {
                prettyPropType = nonNullPropType;
            }

            return prettyPropType;
        }

        public static bool IsNullableEnum(PropertyVisitorContext context, IPropertySymbol symbol)
        {
            return IsNullable(context, symbol, out var innerType) && innerType.TypeKind == TypeKind.Enum;
        }

        public static bool IsNullable(PropertyVisitorContext context, IPropertySymbol symbol, out ITypeSymbol innerType)
        {
            if (symbol.Type is INamedTypeSymbol typeSymbol
                && SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, context.Nullable)
                && typeSymbol.TypeArguments.Length == 1)
            {
                innerType = typeSymbol.TypeArguments[0];
                return true;
            }

            innerType = null;
            return false;
        }

        private const string KustoIgnoreAttributeName = "KustoIgnoreAttribute";

        public static bool IsIgnoredInKusto(IPropertySymbol symbol)
        {
            return symbol.GetAttributes().Any(x => x.AttributeClass.Name == KustoIgnoreAttributeName);
        }

        public static string GetKustoDataType(PropertyVisitorContext context, IPropertySymbol symbol)
        {
            var attributeData = symbol
                .GetAttributes()
                .SingleOrDefault(x => x.AttributeClass.Equals(context.KustoTypeAttribute, SymbolEqualityComparer.Default));
            if (attributeData != null)
            {
                var kustoType = attributeData.ConstructorArguments.Single().Value;
                return kustoType.ToString();
            }

            switch (symbol.Type.ToString())
            {
                case "bool":
                case "bool?":
                    return "bool";
                case "ushort":
                case "ushort?":
                case "short":
                case "short?":
                case "int":
                case "int?":
                    return "int";
                case "float":
                case "float?":
                case "double":
                case "double?":
                    return "real";
                case "uint":
                case "uint?":
                case "long":
                case "long?":
                    return "long";
                case "System.Guid":
                case "System.Guid?":
                    return "guid";
                case "System.TimeSpan":
                case "System.TimeSpan?":
                    return "timespan";
                case "System.DateTimeOffset":
                case "System.DateTimeOffset?":
                    return "datetime";
                case "System.Version":
                case "string":
                    return "string";
                default:
                    if (symbol.Type.TypeKind == TypeKind.Enum || IsNullableEnum(context, symbol))
                    {
                        return "string";
                    }
                    else
                    {
                        return "dynamic";
                    }
            }
        }
    }
}
