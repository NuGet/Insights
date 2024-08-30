// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class ValidatePropertyNullability : IPropertyVisitor
    {
        public void OnProperty(PropertyVisitorContext context, IPropertySymbol symbol, string prettyPropType)
        {
            if (context.HasNoDDLAttribute
                || symbol.Type.IsReferenceType
                || PropertyHelper.IsIgnoredInKusto(symbol)
                || PropertyHelper.IsNullable(context, symbol, out _)
                || PropertyHelper.IsRequired(context, symbol))
            {
                return;
            }

            context.GeneratorExecutionContext.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: DiagnosticIds.NonNullablePropertyNotMarkedAsRequired,
                    title: $"Non-nullable property is not marked as [Required].",
                    messageFormat: $"Non-nullable value type {symbol.ToDisplayString()} is not marked as [Required]. Either make it nullable or mark it as [Required].",
                    CsvRecordGenerator.Category,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                symbol.Locations.FirstOrDefault() ?? Location.None));
        }

        public void Finish(PropertyVisitorContext context)
        {
        }

        public string GetResult()
        {
            throw new NotSupportedException();
        }
    }
}
