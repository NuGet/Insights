// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class WriteHeaderBuilder : IPropertyVisitor
    {
        private readonly int _indent;
        private readonly StringBuilder _builder;

        public WriteHeaderBuilder(int indent)
        {
            _indent = indent;
            _builder = new StringBuilder();
        }

        public void OnProperty(SourceProductionContext context, CsvRecordModel model, CsvPropertyModel property)
        {
            if (_builder.Length == 0)
            {
                _builder.Append(' ', _indent);
                _builder.Append("writer.WriteLine(\"");
            }
            else
            {
                _builder.Append(',');
            }

            _builder.Append(property.Name);
        }

        public void Finish(SourceProductionContext context, CsvRecordModel model)
        {
            _builder.Append("\");");
        }

        public string GetResult()
        {
            return _builder.ToString();
        }
    }
}
