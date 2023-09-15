// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

            builder.AppendLine($"# {_info.TableName}");
            builder.AppendLine($"");
            builder.AppendLine($"|                              |      |");
            builder.AppendLine($"| ---------------------------- | ---- |");
            builder.AppendLine($"| Cardinality                  | TODO |");
            builder.AppendLine($"| Child tables                 | TODO |");
            builder.AppendLine($"| Parent tables                | TODO |");
            builder.AppendLine($"| Column used for partitioning | TODO |");
            builder.AppendLine($"| Data file container name     | {_info.DefaultContainerName} |");
            builder.AppendLine($"| Driver                       | [`TODO`](../drivers/TODO.md) |");
            builder.AppendLine($"| Record type                  | [`{_info.RecordType.Name}`](../../src/Worker.Logic/CatalogScan/Drivers/TODO/{_info.RecordType.Name}.cs) |");
            builder.AppendLine($"");

            var enumAndDynamics = new List<(string name, bool isEnum)>();

            builder.AppendLine($"## Table schema");
            builder.AppendLine($"");
            builder.AppendLine($"| Column name | Data type | Required | Description |");
            builder.AppendLine($"| ----------- | --------- | -------- | ----------- |");
            foreach (var name in _info.NameToIndex.OrderBy(x => x.Value).Select(x => x.Key))
            {
                var property = _info.NameToProperty[name];
                var dataType = TableDocInfo.GetExpectedDataType(property) ?? "TODO";
                builder.AppendLine($"| {name} | {dataType} | TODO | TODO |");

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
                    builder.AppendLine($"");
                    builder.AppendLine($"## {name} schema");
                    builder.AppendLine($"");
                    builder.AppendLine($"| Enum value | Description |");
                    builder.AppendLine($"| ---------- | ----------- |");
                    TableDocInfo.TryGetEnumType(_info.NameToProperty[name].PropertyType, out var enumType);
                    var values = Enum.GetNames(enumType).OrderBy(x => x).ToList();
                    foreach (var value in values)
                    {
                        builder.AppendLine($"| {value} | TODO |");
                    }
                }
                else
                {
                    builder.AppendLine($"");
                    builder.AppendLine($"## {name} schema");
                    builder.AppendLine($"");
                    builder.AppendLine($"TODO");
                }
            }

            builder.AppendLine($"");

            return builder.ToString();
        }
    }
}
