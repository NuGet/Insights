// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

#nullable enable

namespace NuGet.Insights
{
    public class GetBucketKeyProperty : IPropertyVisitor
    {
        private const string AttributeName = "BucketKeyAttribute";
        private const string MethodName = "GetBucketKey";

        private readonly int _indent;
        private string? _bucketKeyName;

        public GetBucketKeyProperty(int indent)
        {
            _indent = indent;
        }

        public bool IsExistingMethod(ISymbol symbol)
        {
            return symbol is IMethodSymbol methodSymbol
                && methodSymbol.Name == MethodName;
        }

        public void OnProperty(PropertyVisitorContext context, IPropertySymbol symbol, string prettyPropType)
        {
            if (!symbol.GetAttributes().Any(x => x.AttributeClass?.Name == AttributeName))
            {
                return;
            }

            var typeString = symbol.Type.ToString();
            if (typeString != "string")
            {
                context.GeneratorExecutionContext.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: DiagnosticIds.BucketKeyNotString,
                        title: $"The property marked as {AttributeName} must be a string.",
                        messageFormat: $"The property marked as {AttributeName} must be a string.",
                        CsvRecordGenerator.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    symbol.Locations.FirstOrDefault() ?? Location.None));
                return;
            }

            if (_bucketKeyName is not null)
            {
                context.GeneratorExecutionContext.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: DiagnosticIds.MultipleBucketKeys,
                        title: $"Multiple {AttributeName} attributes were defined on a single type.",
                        messageFormat: $"Multiple {AttributeName} attributes were defined on a single type.",
                        CsvRecordGenerator.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    symbol.Locations.FirstOrDefault() ?? Location.None));
                return;
            }

            _bucketKeyName = symbol.Name;
        }

        public void Finish(PropertyVisitorContext context)
        {
        }

        public string GetResult()
        {
            if (_bucketKeyName is null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.AppendLine();
            builder.AppendLine();
            builder.Append(' ', _indent);
            builder.AppendFormat("public string {0}()", MethodName);

            builder.AppendLine();
            builder.Append(' ', _indent);
            builder.Append("{");

            builder.AppendLine();
            builder.Append(' ', _indent);
            builder.AppendFormat("    return {0};", _bucketKeyName);

            builder.AppendLine();
            builder.Append(' ', _indent);
            builder.Append("}");

            return builder.ToString();
        }
    }
}
