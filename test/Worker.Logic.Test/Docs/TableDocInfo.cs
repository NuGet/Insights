// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Castle.Core.Internal;
using Humanizer;
using Markdig.Extensions.Tables;
using NuGet.Insights.Worker.CatalogDataToCsv;

#nullable enable

namespace NuGet.Insights.Worker
{
    public class TableDocInfo : DocInfo
    {
        public TableDocInfo(string tableName) : base(Path.Combine("tables", $"{tableName}.md"))
        {
            TableName = tableName;
            RecordType = NuGetInsightsWorkerLogicKustoDDL.TypeToDefaultTableName.Single(x => x.Value == tableName).Key;

            var csvHeaderWriter = new StringWriter();
            RecordType.GetMethod(nameof(ICsvRecord<ICsvRecord>.WriteHeader))!.Invoke(null, [csvHeaderWriter]);

            NameToIndex = csvHeaderWriter
                .ToString()
                .Split(',')
                .Select((x, i) => (x, i))
                .ToDictionary(x => x.x.Trim(), x => x.i);

            var nameToProperty = RecordType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .ToDictionary(x => x.Name);
            nameToProperty.Remove(nameof(ICsvRecord<ICsvRecord>.FieldCount));
            NameToProperty = nameToProperty;

            var settings = new NuGetInsightsWorkerSettings();
            var settingsProperties = settings.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var containerNamePropertyName = $"{tableName.Singularize()}ContainerName";
            Assert.Contains(containerNamePropertyName, settingsProperties.Select(x => x.Name).Where(x => x.EndsWith("ContainerName", StringComparison.Ordinal)));
            DefaultContainerName = Assert.IsType<string>(settingsProperties.Single(x => x.Name == containerNamePropertyName).GetValue(settings));

            CsvPartitioningKeyFieldName = NameToProperty
                .Values
                .Where(x => x.GetCustomAttribute<BucketKeyAttribute>() is not null)
                .FirstOrDefault()?
                .Name;
            KustoPartitioningKeyFieldName = NameToProperty
                .Values
                .Where(x => x.GetCustomAttribute<KustoPartitionKeyAttribute>() is not null)
                .FirstOrDefault()?
                .Name;
            KeyFields = (IReadOnlyList<string>?)RecordType
                .GetProperty(nameof(CatalogLeafItemRecord.KeyFields))?
                .GetValue(obj: null);
        }

        public override void ReadMarkdown()
        {
            if (!File.Exists(DocPath))
            {
                var initializer = new TableDocInitializer(this);
                using (var file = new FileStream(DocPath, FileMode.CreateNew, FileAccess.ReadWrite))
                using (var writer = new StreamWriter(file))
                {
                    writer.Write(initializer.Build());
                }
            }

            base.ReadMarkdown();
        }

        public string TableName { get; }
        public Type RecordType { get; }
        public IReadOnlyDictionary<string, int> NameToIndex { get; }
        public IReadOnlyDictionary<string, PropertyInfo> NameToProperty { get; }
        public string DefaultContainerName { get; }
        public string? CsvPartitioningKeyFieldName { get; }
        public string? KustoPartitioningKeyFieldName { get; }
        public IReadOnlyList<string>? KeyFields { get; }

        public static bool IsDynamic(PropertyInfo property)
        {
            return property.GetAttribute<KustoTypeAttribute>()?.KustoType == "dynamic";
        }

        public List<TableRow> GetFirstTableRows()
        {
            ReadMarkdown();

            var table = MarkdownDocument.OfType<Table>().FirstOrDefault();
            Assert.NotNull(table);

            return table.Cast<TableRow>().ToList();
        }

        public static bool TryGetEnumType(Type type, out Type enumType)
        {
            enumType = type;

            if (type.IsEnum)
            {
                return true;
            }

            if (type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(Nullable<>)
                && type.GenericTypeArguments[0].IsEnum)
            {
                enumType = type.GenericTypeArguments[0];
                return true;
            }

            return false;
        }

        public static string? GetExpectedDataType(PropertyInfo property)
        {
            var propertyType = property.PropertyType;
            if (propertyType == typeof(string) || propertyType == typeof(Guid?) || propertyType == typeof(Version))
            {
                return "string";
            }
            else if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                return "bool";
            }
            else if (propertyType == typeof(ushort) || propertyType == typeof(ushort?))
            {
                return "ushort";
            }
            else if (propertyType == typeof(uint) || propertyType == typeof(uint?))
            {
                return "uint";
            }
            else if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                return "int";
            }
            else if (propertyType == typeof(float) || propertyType == typeof(float?))
            {
                return "float";
            }
            else if (propertyType == typeof(double) || propertyType == typeof(double?))
            {
                return "double";
            }
            else if (propertyType == typeof(long) || propertyType == typeof(long?))
            {
                return "long";
            }
            else if (propertyType == typeof(long) || propertyType == typeof(long?))
            {
                return "long";
            }
            else if (propertyType == typeof(DateTimeOffset) || propertyType == typeof(DateTimeOffset?))
            {
                return "timestamp";
            }
            else if (TryGetEnumType(propertyType, out var enumType))
            {
                if (enumType.GetAttribute<FlagsAttribute>() != null)
                {
                    return "flags enum";
                }
                else
                {
                    return "enum";
                }
            }

            return null;
        }
    }
}
