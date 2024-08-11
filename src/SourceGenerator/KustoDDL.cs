// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static partial class KustoDDL
    {
        private static Dictionary<Type, string> InternalTypeToDefaultTableName;
        private static Dictionary<Type, IReadOnlyList<string>> InternalTypeToDDL;
        private static Dictionary<Type, string> InternalTypeToPartitioningPolicy;

        public static IReadOnlyDictionary<Type, string> TypeToDefaultTableName => InternalTypeToDefaultTableName;
        public static IReadOnlyDictionary<Type, IReadOnlyList<string>> TypeToDDL => InternalTypeToDDL;
        public static IReadOnlyDictionary<Type, string> TypeToPartitioningPolicy => InternalTypeToPartitioningPolicy;
        public const string CsvMappingName = "BlobStorageMapping";

        private static bool AddTypeToDefaultTableName(Type type, string tableName)
        {
            if (InternalTypeToDefaultTableName == null)
            {
                InternalTypeToDefaultTableName = new Dictionary<Type, string>();
            }

            InternalTypeToDefaultTableName.Add(type, tableName);
            return true;
        }

        private static bool AddTypeToDDL(Type type, IReadOnlyList<string> ddl)
        {
            if (InternalTypeToDDL == null)
            {
                InternalTypeToDDL = new Dictionary<Type, IReadOnlyList<string>>();
            }

            InternalTypeToDDL.Add(type, ddl);
            return true;
        }

        private static bool AddTypeToPartitioningPolicy(Type type, string partitioningPolicy)
        {
            if (InternalTypeToPartitioningPolicy == null)
            {
                InternalTypeToPartitioningPolicy = new Dictionary<Type, string>();
            }

            InternalTypeToPartitioningPolicy.Add(type, partitioningPolicy);
            return true;
        }
    }
}
