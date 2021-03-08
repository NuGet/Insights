using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class KustoPartitioningPolicyBuilder : IPropertyVisitor
    {
        private const string AttributeName = "KustoPartitionKeyAttribute";
        private readonly int _intent;
        private readonly StringBuilder _builder;

        public KustoPartitioningPolicyBuilder(int indent)
        {
            _intent = indent;
            _builder = new StringBuilder();
        }

        public void OnProperty(PropertyVisitorContext context, IPropertySymbol symbol, string prettyPropType)
        {
            if (!symbol.GetAttributes().Any(x => x.AttributeClass.Name == AttributeName))
            {
                return;
            }

            var policy = new PartitioningPolicy
            {
                PartitionKeys = new List<PartitionKey>
                {
                    new PartitionKey
                    {
                        ColumnName = symbol.Name,
                        Kind = "Hash",
                        Properties = new PartitionKeyProperties
                        {
                             Function = "XxHash64",
                             MaxPartitionCount = 256,
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(policy, Formatting.Indented);
            var jsonLines = json.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (_builder.Length > 0)
            {
                context.GeneratorExecutionContext.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "EXP0003",
                        title: $"Multiple {AttributeName} attributes were defined on a single type.",
                        messageFormat: $"Multiple {AttributeName} attributes were defined on a single type.",
                        CsvRecordGenerator.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
                return;
            }

            foreach (var line in jsonLines)
            {
                if (_builder.Length > 0)
                {
                    _builder.AppendLine();
                    _builder.Append(' ', _intent);
                }

                var leadingSpaces = Regex.Match(line, "^ *").Value.Length;
                _builder.Append(' ', leadingSpaces);
                _builder.Append("'");
                _builder.Append(line.Substring(leadingSpaces));
                _builder.Append("'");
            }
        }

        public void Finish(PropertyVisitorContext context)
        {
            if (_builder.Length == 0)
            {
                context.GeneratorExecutionContext.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "EXP0003",
                        title: $"No {AttributeName} attributes were defined on a type.",
                        messageFormat: $"No {AttributeName} attributes were defined on a type.",
                        CsvRecordGenerator.Category,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
                return;
            }
        }

        public string GetResult()
        {
            return _builder.ToString();
        }

        private class PartitioningPolicy
        {
            public List<PartitionKey> PartitionKeys { get; set; }
        }

        private class PartitionKey
        {
            public string ColumnName { get; set; }
            public string Kind { get; set; }
            public PartitionKeyProperties Properties { get; set; }
        }

        private class PartitionKeyProperties
        {
            public string Function { get; set; }
            public int MaxPartitionCount { get; set; }
        }
    }
}
