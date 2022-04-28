// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.WideEntities
{
    public class WideEntitySegment : Dictionary<string, object>, ITableEntity
    {
        internal const string SegmentCountPropertyName = "C";

        /// <summary>
        /// The separator between the user-provided wide entity row key and the wide entity index suffix.
        /// </summary>
        public const char RowKeySeparator = '~';

        /// <summary>
        /// The row key suffix for index 0.
        /// </summary>
        internal const string Index0Suffix = "00";

        /// <summary>
        /// 16 properties names.
        /// MAX_ENTITY_SIZE / MAX_BINARY_PROPERTY_SIZE = 1 MiB / 64 KiB = 16
        /// </summary>
        internal static readonly IReadOnlyList<string> ChunkPropertyNames = new[] { "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S" };

        private string _rowKeyPrefix;
        private int _index = -1;

        public static string GetRowKey(string rowKeyPrefix, int index)
        {
            return $"{rowKeyPrefix}{RowKeySeparator}{index:D2}";
        }

        public WideEntitySegment()
        {
            Timestamp = default;
            ETag = default;
        }

        public WideEntitySegment(string partitionKey, string rowKeyPrefix, int index) : this()
        {
            PartitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
            _rowKeyPrefix = rowKeyPrefix;
            _index = index;
            UpdateRowKey();
            if (_index == 0)
            {
                this[SegmentCountPropertyName] = 0;
            }
        }

        public string PartitionKey
        {
            get => (string)this[StorageUtility.PartitionKey];
            set => this[StorageUtility.PartitionKey] = value;
        }

        public string RowKey
        {
            get => (string)this[StorageUtility.RowKey];
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                UpdateRowKeyPrefixAndIndex(value);
                UpdateRowKey();
            }
        }

        public DateTimeOffset? Timestamp
        {
            get => (DateTimeOffset?)this[StorageUtility.Timestamp];
            set => this[StorageUtility.Timestamp] = value;
        }

        public ETag ETag
        {
            get => new ETag((string)this[StorageUtility.ETag]);
            set => this[StorageUtility.ETag] = value.ToString();
        }

        private void UpdateRowKeyPrefixAndIndex(string value)
        {
            var tildeIndex = value.LastIndexOf(RowKeySeparator);
            if (tildeIndex < 0)
            {
                throw new ArgumentException($"The row key must container the separator '{RowKeySeparator}'.", nameof(value));
            }

            _rowKeyPrefix = value.Substring(0, tildeIndex);
            _index = int.Parse(value.Substring(tildeIndex + 1));
        }

        public string RowKeyPrefix
        {
            get
            {
                if (_rowKeyPrefix == null)
                {
                    UpdateRowKeyPrefixAndIndex(RowKey);
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
                    throw new ArgumentException($"The row key prefix cannot contain the separator '{RowKeySeparator}'", nameof(value));
                }

                _rowKeyPrefix = value;
                UpdateRowKey();
            }
        }

        public int Index
        {
            get
            {
                if (_index < 0)
                {
                    UpdateRowKeyPrefixAndIndex(RowKey);
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
                UpdateRowKey();
            }
        }

        private void UpdateRowKey()
        {
            this[StorageUtility.RowKey] = GetRowKey(_rowKeyPrefix, _index);
        }

        public int SegmentCount
        {
            get
            {
                if (Index != 0)
                {
                    throw new InvalidOperationException("The segment count is only available on segments with index 0.");
                }

                return (int)this[SegmentCountPropertyName];
            }

            set
            {
                if (Index != 0)
                {
                    throw new InvalidOperationException("The segment count can only be set on segments with index 0.");
                }

                if (value < 1 || value > StorageUtility.MaxBatchSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"The segment count must be between 0 and {StorageUtility.MaxBatchSize}, inclusive.");
                }

                this[SegmentCountPropertyName] = value;
            }
        }

        private int NonDataPropertyCount => Index == 0 ? 5 : 4;

        public IEnumerable<ReadOnlyMemory<byte>> GetChunks()
        {
            // The first wide entity has a segment count property.
            if (Count > ChunkPropertyNames.Count + NonDataPropertyCount)
            {
                throw new InvalidOperationException($"For segment index {Index}, there cannot be more than {ChunkPropertyNames.Count + NonDataPropertyCount} properties.");
            }

            for (var i = 0; i < Count - NonDataPropertyCount; i++)
            {
                if (!TryGetValue(ChunkPropertyNames[i], out var property))
                {
                    throw new InvalidOperationException($"Property '{ChunkPropertyNames[i]}' could not be found.");
                }

                if (property is byte[] byteArray)
                {
                    yield return byteArray.AsMemory();
                }
                else if (property is BinaryData binaryValue)
                {
                    yield return binaryValue.ToMemory();
                }
                else
                {
                    throw new InvalidOperationException($"Property '{ChunkPropertyNames[i]}' must be binary.");
                }
            }
        }

        public void AddChunk(ReadOnlyMemory<byte> chunk)
        {
            this[ChunkPropertyNames[Count - NonDataPropertyCount]] = new BinaryData(chunk);
        }
    }
}
