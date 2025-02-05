// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class ValidatePropertyNullability : IPropertyVisitor
    {
        public void OnProperty(SourceProductionContext context, CsvRecordModel model, CsvPropertyModel property)
        {
            if (string.IsNullOrEmpty(model.KustoDDLName)
                || property.IsReferenceType
                || property.IsKustoIgnore
                || property.IsNullable
                || property.IsRequired)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: DiagnosticIds.NonNullablePropertyNotMarkedAsRequired,
                    title: $"Non-nullable property is not marked as [Required].",
                    messageFormat: $"Non-nullable value type {property.FullName} is not marked as [Required]. Either make it nullable or mark it as [Required].",
                    Constants.Category,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                property.Locations.FirstOrDefault() ?? Location.None));
        }

        public void Finish(SourceProductionContext context, CsvRecordModel model)
        {
        }

        public string GetResult()
        {
            throw new NotSupportedException();
        }
    }
}
