// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NuGet.Insights.Worker
{
    public class TableDocInitializer
    {
        private readonly TableDocInfo _info;

        public TableDocInitializer(TableDocInfo info)
        {
            _info = info;
        }

        public string Build()
        {
            var builder = new StringBuilder();

            builder.AppendLine(CultureInfo.InvariantCulture, $"# {_info.TableName}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"");
            builder.AppendLine(CultureInfo.InvariantCulture, $"|                              |      |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| ---------------------------- | ---- |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Cardinality                  | TODO |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Child tables                 | TODO |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Parent tables                | TODO |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Column used for partitioning | TODO |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Data file container name     | {_info.DefaultContainerName} |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Driver                       | [`TODO`](../drivers/TODO.md) |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Record type                  | [`{_info.RecordType.Name}`](../../src/Worker.Logic/CatalogScan/Drivers/TODO/{_info.RecordType.Name}.cs) |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"");

            var enumAndDynamics = new List<(string name, bool isEnum)>();

            builder.AppendLine($"## Table schema");
            builder.AppendLine($"");
            builder.AppendLine($"| Column name | Data type | Required | Description |");
            builder.AppendLine($"| ----------- | --------- | -------- | ----------- |");
            foreach (var name in _info.NameToIndex.OrderBy(x => x.Value).Select(x => x.Key))
            {
                var property = _info.NameToProperty[name];
                var dataType = TableDocInfo.GetExpectedDataType(property) ?? "TODO";
                builder.AppendLine(CultureInfo.InvariantCulture, $"| {name} | {dataType} | TODO | TODO |");

                if (TableDocInfo.TryGetEnumType(property.PropertyType, out var _))
                {
                    enumAndDynamics.Add((name, true));
                }

                if (TableDocInfo.IsDynamic(property))
                {
                    enumAndDynamics.Add((name, false));
                }
            }

            foreach ((var name, var isEnum) in enumAndDynamics)
            {
                if (isEnum)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"");
                    builder.AppendLine(CultureInfo.InvariantCulture, $"## {name} schema");
                    builder.AppendLine(CultureInfo.InvariantCulture, $"");
                    builder.AppendLine(CultureInfo.InvariantCulture, $"| Enum value | Description |");
                    builder.AppendLine(CultureInfo.InvariantCulture, $"| ---------- | ----------- |");
                    TableDocInfo.TryGetEnumType(_info.NameToProperty[name].PropertyType, out var enumType);
                    var values = Enum.GetNames(enumType).OrderBy(x => x, StringComparer.Ordinal).ToList();
                    foreach (var value in values)
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"| {value} | TODO |");
                    }
                }
                else
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"");
                    builder.AppendLine(CultureInfo.InvariantCulture, $"## {name} schema");
                    builder.AppendLine(CultureInfo.InvariantCulture, $"");
                    builder.AppendLine(CultureInfo.InvariantCulture, $"TODO");
                }
            }

            builder.AppendLine($"");

            return builder.ToString();
        }
    }
}
