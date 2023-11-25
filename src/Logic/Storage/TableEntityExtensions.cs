// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;

namespace NuGet.Insights
{
    public static class TableEntityExtensions
    {
        public static int GetEntitySize(this ITableEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity is IEnumerable<KeyValuePair<string, object>> properties)
            {
                return GetEntitySize(properties);
            }

            throw new NotImplementedException();
        }

        private static int GetEntitySize(IEnumerable<KeyValuePair<string, object>> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            var calculator = new TableEntitySizeCalculator();
            calculator.AddEntityOverhead();

            foreach (var property in properties)
            {
                switch (property.Key)
                {
                    case StorageUtility.PartitionKey:
                        calculator.AddPartitionKey((string)property.Value);
                        break;
                    case StorageUtility.RowKey:
                        calculator.AddRowKey((string)property.Value);
                        break;
                    case StorageUtility.Timestamp:
                    case StorageUtility.ETag:
                        // Skip these since they are not serialized as user properties.
                        break;
                    default:
                        calculator.AddProperty(property.Key, property.Value);
                        break;
                }
            }

            return calculator.Size;
        }
    }
}
