using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.WideEntities
{
    internal class WideEntitySegment : ITableEntity
    {
        internal const string SegmentCountPropertyName = "C";

        /// <summary>
        /// The separator between the user-provided wide entity row key and the wide entity index suffix.
        /// </summary>
        internal const char RowKeySeparator = '~';

        /// <summary>
        /// The row key suffix for index 0.
        /// </summary>
        internal const string Index0Suffix = "00";

        /// <summary>
        /// 16 properties names.
        /// MAX_ENTITY_SIZE / MAX_BINARY_PROPERTY_SIZE = 1 MiB / 64 KiB = 16
        /// </summary>
        private static readonly IReadOnlyList<string> ChunkPropertyNames = new[] { "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S" };

        private string _rowKey;
        private string _rowKeyPrefix;
        private int _index = -1;
        private int _segmentCount;

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
                    // We use "D2" since we will never have more than 2 digits: 00 - 99. This allows for up to 100
                    // segments per wide entity. We use 100 because this is the maximum batch size allowed in Azure
                    // Table Storage.
                    _rowKey = $"{RowKeyPrefix}{RowKeySeparator}{_index:D2}";
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

                if (value.Contains(RowKeySeparator))
                {
                    throw new ArgumentException($"The row key prefix cannot contain the separator '{RowKeySeparator}'");
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
                if (value < 0 || value > StorageUtility.MaxBatchSize - 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"The index must be between 0 and {StorageUtility.MaxBatchSize - 1}, inclusive.");
                }

                _index = value;
            }
        }

        public DateTimeOffset Timestamp { get; set; }
        public string ETag { get; set; }
        public int SegmentCount
        {
            get
            {
                if (Index != 0)
                {
                    throw new InvalidOperationException("The segment count is only available on segments with index 0.");
                }

                return _segmentCount;
            }

            set
            {
                if (Index != 0)
                {
                    throw new InvalidOperationException("The segment count can only be set on segments with index 0.");
                }

                if (value < 1 || value > StorageUtility.MaxBatchSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"The segment count must be between 1 and {StorageUtility.MaxBatchSize}, inclusive.");
                }

                _segmentCount = value;
            }
        }

        public List<ReadOnlyMemory<byte>> Chunks { get; } = new List<ReadOnlyMemory<byte>>();

        private int GetSeparatorIndex(string value)
        {
            var tildeIndex = value.LastIndexOf(RowKeySeparator);

            if (tildeIndex < 0)
            {
                throw new InvalidDataException($"The row key must container the separator '{RowKeySeparator}'.");
            }

            return tildeIndex;
        }

        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            // The first wide entity has a segment count property.
            var nonDataPropertyCount = Index == 0 ? 1 : 0;
            if (properties.Count > ChunkPropertyNames.Count + nonDataPropertyCount)
            {
                throw new ArgumentException($"For segment index {Index}, there cannot be more than {ChunkPropertyNames.Count + nonDataPropertyCount} properties.", nameof(properties));
            }

            if (Index == 0)
            {
                if (!properties.TryGetValue(SegmentCountPropertyName, out var property))
                {
                    throw new ArgumentException($"Property '{SegmentCountPropertyName}' could not be found.", nameof(properties));
                }

                if (property.PropertyType != EdmType.Int32)
                {
                    throw new ArgumentException($"Property '{SegmentCountPropertyName}' must be an Int32.", nameof(properties));
                }

                SegmentCount = property.Int32Value.Value;
            }

            Chunks.Clear();
            for (var i = 0; i < properties.Count - nonDataPropertyCount; i++)
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

            if (Index == 0)
            {
                dictionary.Add(SegmentCountPropertyName, new EntityProperty(SegmentCount));
            }

            for (var i = 0; i < Chunks.Count; i++)
            {
                dictionary.Add(ChunkPropertyNames[i], new EntityProperty(Chunks[i].ToArray()));
            }

            return dictionary;
        }
    }
}
