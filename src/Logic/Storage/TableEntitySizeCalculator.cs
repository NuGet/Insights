using System;

namespace Knapcode.ExplorePackages
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/archive/blogs/avkashchauhan/how-the-size-of-an-entity-is-caclulated-in-windows-azure-table-storage
    /// </summary>
    public class TableEntitySizeCalculator
    {
        // Entity overhead of 4 plus a magic, discovered value.
        // See: https://github.com/MicrosoftDocs/azure-docs/issues/68661
        private const int InitialSize = 4 + (88 + 48 + 16 + 8);

        public int Size { get; private set; }

        public void Reset()
        {
            Size = 0;
        }

        public void AddEntityOverhead()
        {
            Size += InitialSize;
        }

        public void AddPartitionKey(string rowKey)
        {
            AddPartitionKey(rowKey.Length);
        }

        public void AddPartitionKey(int length)
        {
            Size += length * 2;
        }

        public void AddRowKey(string rowKey)
        {
            AddRowKey(rowKey.Length);
        }

        public void AddRowKey(int length)
        {
            Size += length * 2;
        }

        public void AddPropertyOverhead(int nameLength)
        {
            Size += GetPropertyOverhead(nameLength);
        }

        public void AddBinaryData(int binaryLength)
        {
            Size += GetBinaryDataSize(binaryLength);
        }

        public void AddInt32Data()
        {
            Size += GetInt32DataSize();
        }

        public void AddProperty(string name, object value)
        {
            Size += GetEntityPropertySize(name.Length, value);
        }

        private static int GetEntityPropertySize(int nameLength, object value)
        {
            if (value is null)
            {
                return 0;
            }

            int size;
            switch (value)
            {
                case string stringValue:
                    size = stringValue.Length * 2 + 4;
                    break;
                case DateTimeOffset:
                    size = 8;
                    break;
                case Guid:
                    size = 16;
                    break;
                case double:
                    size = 8;
                    break;
                case int:
                    size = GetInt32DataSize();
                    break;
                case long:
                    size = 8;
                    break;
                case bool:
                    size = 1;
                    break;
                case byte[] binaryValue:
                    size = GetBinaryDataSize(binaryValue.Length);
                    break;

                default:
                    throw new NotImplementedException();
            };

            return GetPropertyOverhead(nameLength) + size;
        }

        private static int GetPropertyOverhead(int nameLength)
        {
            return 8 + nameLength * 2;
        }

        private static int GetInt32DataSize()
        {
            return 4;
        }

        private static int GetBinaryDataSize(int binaryLength)
        {
            return binaryLength + 4;
        }
    }
}
