// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            if (prettyPropType.StartsWith(systemPrefix, StringComparison.Ordinal) && prettyPropType.IndexOf('.', systemPrefix.Length) < 0)
            {
                prettyPropType = prettyPropType.Substring(systemPrefix.Length);
            }
            else if (prettyPropType.StartsWith(classNamespacePrefix, StringComparison.Ordinal))
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

        public static string GetKustoDataType(CsvPropertyModel property)
        {
            if (property.KustoType != null)
            {
                return property.KustoType;
            }

            switch (property.Type)
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
                    if (property.IsEnum || property.IsNullableEnum)
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
