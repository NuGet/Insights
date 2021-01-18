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

            var calculator = new TableEntitySizeCalculator();
            calculator.AddPartitionKeyRowKey(partitionKey, rowKey);

            foreach (var property in properties)
            {
                calculator.AddProperty(property.Key, property.Value);
            }

            return calculator.Size;
        }

    }
}
