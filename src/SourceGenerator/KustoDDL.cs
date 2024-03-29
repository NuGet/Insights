// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static partial class KustoDDL
    {
        private static Dictionary<Type, string> _typeToDefaultTableName;
        private static Dictionary<Type, IReadOnlyList<string>> _typeToDDL;
        private static Dictionary<Type, string> _typeToPartitioningPolicy;

        public static IReadOnlyDictionary<Type, string> TypeToDefaultTableName => _typeToDefaultTableName;
        public static IReadOnlyDictionary<Type, IReadOnlyList<string>> TypeToDDL => _typeToDDL;
        public static IReadOnlyDictionary<Type, string> TypeToPartitioningPolicy => _typeToPartitioningPolicy;
        public const string CsvMappingName = "BlobStorageMapping";

        private static bool AddTypeToDefaultTableName(Type type, string tableName)
        {
            if (_typeToDefaultTableName == null)
            {
                _typeToDefaultTableName = new Dictionary<Type, string>();
            }

            _typeToDefaultTableName.Add(type, tableName);
            return true;
        }

        private static bool AddTypeToDDL(Type type, IReadOnlyList<string> ddl)
        {
            if (_typeToDDL == null)
            {
                _typeToDDL = new Dictionary<Type, IReadOnlyList<string>>();
            }

            _typeToDDL.Add(type, ddl);
            return true;
        }

        private static bool AddTypeToPartitioningPolicy(Type type, string partitioningPolicy)
        {
            if (_typeToPartitioningPolicy == null)
            {
                _typeToPartitioningPolicy = new Dictionary<Type, string>();
            }

            _typeToPartitioningPolicy.Add(type, partitioningPolicy);
            return true;
        }
    }
}
