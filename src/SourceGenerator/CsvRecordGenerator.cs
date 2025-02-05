// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Humanizer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace NuGet.Insights
{
    [Generator]
    public class CsvRecordGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // find all records with the [CsvRecord] attribute
            var records = context
                .SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "NuGet.Insights.CsvRecordAttribute",
                    predicate: (_, _) => true,
                    transform: TransformCsvRecord)
                .WithTrackingName("ExtractCsvRecords");
            context.RegisterSourceOutput(records, ProduceSourceForIndividualCsvRecord);

            // collect all records into one generator input so we can add source files shared by all records
            var collectedRecords = records
                .Where(r => r.Model is not null)
                .Collect()
                .WithTrackingName("CollectForSharedSources");
            context.RegisterSourceOutput(collectedRecords, ProduceSharedSource);
        }

        private static CsvRecordResult TransformCsvRecord(GeneratorAttributeSyntaxContext context, CancellationToken token)
        {
            string typeKeyword;
            TypeDeclarationSyntax syntax;
            switch (context.TargetNode)
            {
                case ClassDeclarationSyntax classDeclaration:
                    typeKeyword = "class";
                    syntax = classDeclaration;
                    break;
                case RecordDeclarationSyntax recordDeclaration:
                    typeKeyword = "record";
                    syntax = recordDeclaration;
                    break;
                default:
                    return new(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            id: DiagnosticIds.InvalidTargetType,
                            title: "The [CsvRecord] type must be a class or a record.",
                            messageFormat: "The [CsvRecord] type must be a class or a record.",
                            Constants.Category,
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        context.TargetNode.GetLocation() ?? Location.None));
            }

            if (!syntax.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                return new(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: DiagnosticIds.TargetTypeMustBePublic,
                        title: "The [CsvRecord] type must be public.",
                        messageFormat: "The [CsvRecord] type must be public.",
                        Constants.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    context.TargetNode.GetLocation() ?? Location.None));
            }

            if (syntax.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {
                return new(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: DiagnosticIds.TargetTypeMustNotBeAbstract,
                        title: "The [CsvRecord] type must not be abstract.",
                        messageFormat: "The [CsvRecord] type must not be abstract.",
                        Constants.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    context.TargetNode.GetLocation() ?? Location.None));
            }

            if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return new(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: DiagnosticIds.TargetTypeMustBePartial,
                        title: "The [CsvRecord] type must be partial.",
                        messageFormat: "The [CsvRecord] type must be partial.",
                        Constants.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    context.TargetNode.GetLocation() ?? Location.None));
            }

            bool noKustoDDL = false;
            foreach (var attributeListSyntax in syntax.AttributeLists)
            {
                foreach (var attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                    {
                        continue;
                    }

                    var attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                    var fullName = attributeContainingTypeSymbol.ToDisplayString();

                    if (fullName == "NuGet.Insights.NoKustoDDLAttribute")
                    {
                        noKustoDDL = true;
                    }
                }
            }

            var symbol = (INamedTypeSymbol)context.TargetSymbol;
            var propertyModels = GetPropertyModelsFromRecordSymbol(symbol);

            string? kustoDDLName;
            if (noKustoDDL)
            {
                kustoDDLName = null;
            }
            else
            {
                if (context.SemanticModel.Compilation.AssemblyName is null)
                {
                    kustoDDLName = "KustoDDL";
                }
                else
                {
                    kustoDDLName = context.SemanticModel.Compilation.AssemblyName.Replace(".", string.Empty) + "KustoDDL";
                }
            }

            return new(new CsvRecordModel(
                AssemblyName: context.SemanticModel.Compilation.AssemblyName,
                kustoDDLName,
                Namespace: symbol.ContainingNamespace.ToDisplayString(),
                TypeKeyword: typeKeyword,
                Name: symbol.Name,
                Locations: symbol.Locations,
                Properties: new EquatableList<CsvPropertyModel>(propertyModels)));
        }

        private static List<CsvPropertyModel> GetPropertyModelsFromRecordSymbol(INamedTypeSymbol symbol)
        {
            var sortedProperties = GetSortedPropertySymbolsFromRecordSymbol(symbol);

            var allPropertyNames = sortedProperties.Select(x => x.Name).ToImmutableHashSet();
            var typeNamespacePrefix = symbol.ContainingNamespace.ToString() + ".";

            var propertyModels = new List<CsvPropertyModel>();
            foreach (var propertySymbol in sortedProperties)
            {
                propertyModels.Add(GetModelFromPropertySymbol(allPropertyNames, typeNamespacePrefix, propertySymbol));
            }

            return propertyModels;
        }

        private static List<IPropertySymbol> GetSortedPropertySymbolsFromRecordSymbol(INamedTypeSymbol symbol)
        {
            var sortedProperties = new List<IPropertySymbol>();
            var currentType = symbol;

            while (currentType != null)
            {
                sortedProperties.AddRange(currentType
                    .GetMembers()
                    .Where(x => !x.IsImplicitlyDeclared)
                    .Where(x => !x.IsStatic)
                    .OfType<IPropertySymbol>()
                    .OrderByDescending(x => x.Locations.First().SourceSpan.Start));

                currentType = currentType.BaseType;
            }

            sortedProperties.Reverse();
            return sortedProperties;
        }

        private static CsvPropertyModel GetModelFromPropertySymbol(ImmutableHashSet<string> propertyNames, string typeNamespacePrefix, IPropertySymbol symbol)
        {
            var prettyType = PropertyHelper.GetPrettyType(typeNamespacePrefix, propertyNames, symbol);

            bool isRequired = false;
            bool isBucketKey = false;
            bool isKustoPartitionKey = false;
            bool isKustoIgnore = false;
            string? kustoType = null;
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass is null)
                {
                    continue;
                }

                var displayName = attribute.AttributeClass.ToDisplayString();

                switch (displayName)
                {
                    case "System.ComponentModel.DataAnnotations.RequiredAttribute":
                        isRequired = true;
                        break;
                    case "NuGet.Insights.BucketKeyAttribute":
                        isBucketKey = true;
                        break;
                    case "NuGet.Insights.KustoPartitionKeyAttribute":
                        isKustoPartitionKey = true;
                        break;
                    case "NuGet.Insights.KustoIgnoreAttribute":
                        isKustoIgnore = true;
                        break;
                    case "NuGet.Insights.KustoTypeAttribute":
                        kustoType = attribute.ConstructorArguments.Single().Value!.ToString();
                        break;
                }
            }

            ITypeSymbol? nullableType = null;
            bool isNullableEnum = false;
            if (symbol.Type is INamedTypeSymbol typeSymbol
                && typeSymbol.OriginalDefinition.ToDisplayString() == "System.Nullable<T>"
                && typeSymbol.TypeArguments.Length == 1)
            {
                nullableType = typeSymbol.TypeArguments[0];
                isNullableEnum = nullableType.TypeKind == TypeKind.Enum;
            }

            return new CsvPropertyModel(
                FullName: symbol.ToDisplayString(),
                Name: symbol.Name,
                Type: symbol.Type.ToDisplayString(),
                PrettyType: prettyType,
                Locations: symbol.Locations,
                IsNullable: nullableType is not null,
                IsNullableEnum: isNullableEnum,
                IsEnum: symbol.Type.TypeKind == TypeKind.Enum,
                IsReferenceType: symbol.Type.IsReferenceType,
                IsBucketKey: isBucketKey,
                IsKustoIgnore: isKustoIgnore,
                IsRequired: isRequired,
                IsKustoPartitionKey: isKustoPartitionKey,
                KustoType: kustoType);
        }

        private static void ProduceSourceForIndividualCsvRecord(SourceProductionContext context, CsvRecordResult result)
        {
            if (result.Diagnostic is not null)
            {
                context.ReportDiagnostic(result.Diagnostic);
                return;
            }

            var model = result.Model!;

            var kustoTableCommentBuilder = new KustoTableBuilder(indent: 8);
            var kustoPartitioningPolicyCommentBuilder = new KustoPartitioningPolicyBuilder(indent: 4, escapeQuotes: false);
            var kustoMappingCommentBuilder = new KustoMappingBuilder(indent: 8, escapeQuotes: false);
            var writeHeaderBuilder = new WriteHeaderBuilder(indent: 12);
            var writeListBuilder = new WriteListBuilder(indent: 12);
            var writeTextWriterBuilder = new WriteTextWriterBuilder(indent: 12);
            var writeAsyncTextWriterBuilder = new WriteAsyncTextWriterBuilder(indent: 12);
            var readerBuilder = new ReadBuilder(indent: 16);
            var setEmptyStringsBuilder = new SetEmptyStringsBuilder(indent: 12);
            var kustoTableConstantBuilder = new KustoTableBuilder(indent: 16);
            var kustoPartitioningPolicyConstantBuilder = new KustoPartitioningPolicyBuilder(indent: 12, escapeQuotes: false);
            var kustoMappingConstantBuilder = new KustoMappingBuilder(indent: 16, escapeQuotes: false);
            var validatePropertyNullability = new ValidatePropertyNullability();
            var findBucketKeyProperty = new GetBucketKeyProperty(indent: 8);

            var visitors = new List<IPropertyVisitor>
            {
                kustoTableCommentBuilder,
                kustoPartitioningPolicyCommentBuilder,
                kustoMappingCommentBuilder,
                writeHeaderBuilder,
                writeListBuilder,
                writeTextWriterBuilder,
                writeAsyncTextWriterBuilder,
                readerBuilder,
                setEmptyStringsBuilder,
                kustoTableConstantBuilder,
                kustoPartitioningPolicyConstantBuilder,
                kustoMappingConstantBuilder,
                validatePropertyNullability,
                findBucketKeyProperty,
            };


            foreach (var property in model.Properties)
            {
                foreach (var visitor in visitors)
                {
                    visitor.OnProperty(context, model, property);
                }
            }

            foreach (var visitor in visitors)
            {
                visitor.Finish(context, model);
            }

            var kustoTableName = model.Name;
            var suffixesToRemove = new[]
            {
                "CsvRecord",
                "Record",
            }.OrderByDescending(x => x.Length).ThenBy(x => x, StringComparer.Ordinal);

            foreach (var suffix in suffixesToRemove)
            {
                if (kustoTableName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    kustoTableName = kustoTableName.Substring(0, kustoTableName.Length - suffix.Length);
                }
            }
            kustoTableName = kustoTableName.Pluralize();

            context.AddSource(
                $"{model.Name}.ICsvRecord.cs",
                SourceText.From(
                    string.Format(
                        Constants.CsvRecordTemplate,
                        model.Namespace,
                        kustoTableName,
                        kustoTableCommentBuilder.GetResult(),
                        kustoPartitioningPolicyCommentBuilder.GetResult(),
                        kustoMappingCommentBuilder.GetResult(),
                        model.TypeKeyword,
                        model.Name,
                        model.Properties.Count,
                        writeHeaderBuilder.GetResult(),
                        writeListBuilder.GetResult(),
                        writeTextWriterBuilder.GetResult(),
                        writeAsyncTextWriterBuilder.GetResult(),
                        readerBuilder.GetResult(),
                        Constants.KustoCsvMappingName,
                        setEmptyStringsBuilder.GetResult(),
                        findBucketKeyProperty.GetResult()),
                    Encoding.UTF8));

            if (!string.IsNullOrEmpty(model.KustoDDLName))
            {
                context.AddSource(
                    $"{model.Name}.KustoDDL.cs",
                    SourceText.From(
                        string.Format(
                            Constants.KustoDDLTemplate,
                            model.Namespace,
                            kustoTableName,
                            kustoTableConstantBuilder.GetResult(),
                            model.Name,
                            kustoPartitioningPolicyConstantBuilder.GetResult(),
                            kustoMappingConstantBuilder.GetResult(),
                            Constants.KustoCsvMappingName,
                            model.KustoDDLName),
                        Encoding.UTF8));
            }
        }

        private static void ProduceSharedSource(SourceProductionContext context, ImmutableArray<CsvRecordResult> results)
        {
            var kustoDDLNames = new HashSet<string>();
            foreach (var result in results)
            {
                if (!string.IsNullOrEmpty(result.Model?.KustoDDLName))
                {
                    kustoDDLNames.Add(result.Model!.KustoDDLName!);
                }
            }

            foreach (var name in kustoDDLNames.OrderBy(x => x))
            {
                context.AddSource(
                    $"{name}.cs",
                    SourceText.From(
                        string.Format(
                            Constants.KustoDDLMainTemplate,
                            name,
                            Constants.KustoCsvMappingName),
                        Encoding.UTF8));
            }
        }
    }
}
