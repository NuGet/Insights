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

        public void OnProperty(SourceProductionContext context, CsvRecordModel model, CsvPropertyModel property)
        {
            if (!property.IsBucketKey)
            {
                return;
            }

            if (property.PrettyType != "string")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: DiagnosticIds.BucketKeyNotString,
                        title: $"The property marked as {AttributeName} must be a string.",
                        messageFormat: $"The property marked as {AttributeName} must be a string.",
                        Constants.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    property.Locations.FirstOrDefault() ?? Location.None));
                return;
            }

            if (_bucketKeyName is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: DiagnosticIds.MultipleBucketKeys,
                        title: $"Multiple {AttributeName} attributes were defined on a single type.",
                        messageFormat: $"Multiple {AttributeName} attributes were defined on a single type.",
                        Constants.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    property.Locations.FirstOrDefault() ?? Location.None));
                return;
            }

            _bucketKeyName = property.Name;
        }

        public void Finish(SourceProductionContext context, CsvRecordModel model)
        {
            if (_bucketKeyName is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: DiagnosticIds.NoBucketKeyDefined,
                        title: $"No {AttributeName} attribute was defined on the type.",
                        messageFormat: $"No {AttributeName} attribute was defined on the type.",
                        Constants.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    model.Locations.FirstOrDefault() ?? Location.None));
            }
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
