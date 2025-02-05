// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class KustoTableBuilder : IPropertyVisitor
    {
        private readonly int _indent;
        private readonly StringBuilder _builder;

        public KustoTableBuilder(int indent)
        {
            _indent = indent;
            _builder = new StringBuilder();
        }

        public void OnProperty(SourceProductionContext context, CsvRecordModel model, CsvPropertyModel property)
        {
            if (property.IsKustoIgnore)
            {
                return;
            }

            if (_builder.Length > 0)
            {
                _builder.Append(',');
                _builder.AppendLine();
            }

            _builder.Append(' ', _indent);
            _builder.AppendFormat("{0}: {1}", property.Name, PropertyHelper.GetKustoDataType(property));
        }

        public void Finish(SourceProductionContext context, CsvRecordModel model)
        {
        }

        public string GetResult()
        {
            return _builder.ToString();
        }
    }
}
