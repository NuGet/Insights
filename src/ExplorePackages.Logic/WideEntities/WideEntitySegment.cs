using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntitySegment : ITableEntity
    {
        /// <summary>
        /// 16 properties names.
        /// MAX_ENTITY_SIZE / MAX_BINARY_PROPERTY_SIZE = 1 MiB / 64 KiB = 16
        /// </summary>
        private static readonly IReadOnlyList<string> ChunkPropertyNames = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P" };

        private string _rowKey;
        private string _rowKeyPrefix;
        private int _index = -1;

        public WideEntitySegment()
        {
        }

        public WideEntitySegment(string partitionKey, string rowKeyPrefix, int index)
        {
            PartitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
            RowKeyPrefix = rowKeyPrefix;
            Index = index;
        }

        public string PartitionKey { get; set; }

        public string RowKey
        {
            get
            {
                if (_rowKey == null)
                {
                    _rowKey = $"{RowKeyPrefix}{WideEntityService.RowKeySeparator}{_index:D2}";
                }

                return _rowKey;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                var tildeIndex = GetSeparatorIndex(value);
                RowKeyPrefix = value.Substring(0, tildeIndex);
                _index = int.Parse(value.Substring(tildeIndex + 1));
            }
        }

        public string RowKeyPrefix
        {
            get
            {
                if (_rowKeyPrefix == null)
                {
                    var tildeIndex = GetSeparatorIndex(RowKey);
                    _rowKeyPrefix = RowKey.Substring(0, tildeIndex);
                }

                return _rowKeyPrefix;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Contains(WideEntityService.RowKeySeparator))
                {
                    throw new ArgumentException($"The row key prefix cannot contain the separator '{WideEntityService.RowKeySeparator}'");
                }

                _rowKeyPrefix = value;
            }
        }

        public int Index
        {
            get
            {
                if (_index < 0)
                {
                    var tildeIndex = GetSeparatorIndex(RowKey);
                    _index = int.Parse(RowKey.Substring(tildeIndex + 1));
                }

                return _index;
            }

            set
            {
                if (value < 0 || value > 99)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The index must be between 0 and 99, inclusive.");
                }

                _index = value;
            }
        }

        public DateTimeOffset Timestamp { get; set; }
        public string ETag { get; set; }
        public List<ReadOnlyMemory<byte>> Chunks { get; } = new List<ReadOnlyMemory<byte>>();

        private int GetSeparatorIndex(string value)
        {
            var tildeIndex = value.LastIndexOf(WideEntityService.RowKeySeparator);

            if (tildeIndex < 0)
            {
                throw new InvalidDataException($"The row key must container the separator '{WideEntityService.RowKeySeparator}'.");
            }

            return tildeIndex;
        }

        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            if (properties.Count > ChunkPropertyNames.Count)
            {
                throw new ArgumentException($"There cannot be more than {ChunkPropertyNames.Count} properties.", nameof(properties));
            }

            Chunks.Clear();
            for (var i = 0; i < properties.Count; i++)
            {
                if (!properties.TryGetValue(ChunkPropertyNames[i], out var property))
                {
                    throw new ArgumentException($"Property '{ChunkPropertyNames[i]}' could not be found.", nameof(properties));
                }

                if (property.PropertyType != EdmType.Binary)
                {
                    throw new ArgumentException($"Property '{ChunkPropertyNames[i]}' must be binary.", nameof(properties));
                }

                Chunks.Add(property.BinaryValue.AsMemory());
            }
        }

        public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            if (Chunks.Count > ChunkPropertyNames.Count)
            {
                throw new InvalidOperationException($"There cannot be more than {ChunkPropertyNames.Count} chunks.");
            }

            var dictionary = new Dictionary<string, EntityProperty>();
            for (var i = 0; i < Chunks.Count; i++)
            {
                dictionary.Add(ChunkPropertyNames[i], new EntityProperty(Chunks[i].ToArray()));
            }

            return dictionary;
        }
    }
}
