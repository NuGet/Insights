using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages
{
    public static class TableEntityExtensions
    {
        public static int GetEntitySize(this ITableEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity is DynamicTableEntity dte)
            {
                return GetEntitySize(dte.PartitionKey, dte.RowKey, dte.Properties);
            }

            var properties = entity.WriteEntity(operationContext: null);

            return GetEntitySize(entity.PartitionKey, entity.RowKey, properties);
        }

        /// <summary>
        /// Source: https://docs.microsoft.com/en-us/archive/blogs/avkashchauhan/how-the-size-of-an-entity-is-caclulated-in-windows-azure-table-storage
        /// </summary>
        private static int GetEntitySize(string partitionKey, string rowKey, IEnumerable<KeyValuePair<string, EntityProperty>> properties)
        {
            if (partitionKey == null)
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            var size = 4 + (partitionKey.Length + rowKey.Length) * 2;

            foreach (var property in properties)
            {
                size += GetEntityPropertySize(property.Key, property.Value);
            }

            // See: https://github.com/MicrosoftDocs/azure-docs/issues/68661
            size += 88;

            return size;
        }

        private static int GetEntityPropertySize(string name, EntityProperty value)
        {
            int size;
            switch (value.PropertyType)
            {
                case EdmType.String when value.StringValue == null:
                    return 0;
                case EdmType.DateTime when !value.DateTime.HasValue:
                    return 0;
                case EdmType.Guid when !value.GuidValue.HasValue:
                    return 0;
                case EdmType.Double when !value.DoubleValue.HasValue:
                    return 0;
                case EdmType.Int32 when !value.Int32Value.HasValue:
                    return 0;
                case EdmType.Int64 when !value.Int64Value.HasValue:
                    return 0;
                case EdmType.Boolean when !value.BooleanValue.HasValue:
                    return 0;
                case EdmType.Binary when value.BinaryValue == null:
                    return 0;

                case EdmType.String:
                    size = value.StringValue.Length * 2 + 4;
                    break;
                case EdmType.DateTime:
                    size = 8;
                    break;
                case EdmType.Guid:
                    size = 16;
                    break;
                case EdmType.Double:
                    size = 8;
                    break;
                case EdmType.Int32:
                    size = 4;
                    break;
                case EdmType.Int64:
                    size = 8;
                    break;
                case EdmType.Boolean:
                    size = 1;
                    break;
                case EdmType.Binary:
                    size = value.BinaryValue.Length + 4;
                    break;

                default:
                    throw new NotImplementedException();
            };

            return 8 + name.Length * 2 + size;
        }
    }
}
