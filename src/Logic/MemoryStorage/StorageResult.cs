// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public enum StorageResultType
    {
        Success,
        BlockedByLease,
        BlockedByDifferentLease,
        DoesNotExist,
        AlreadyExists,
        HasNoLease,
        ETagMismatch,
        HashMismatch,
    }

    public class StorageResult<T>
    {
        private readonly T? _value;
        private bool _hasValue;

        public StorageResult(StorageResultType type, T value)
        {
            Type = type;
            _value = value;
            _hasValue = true;
        }

        public StorageResult(StorageResultType type)
        {
            Type = type;
            _hasValue = false;
        }

        public StorageResultType Type { get; }
        public T Value => _hasValue ? _value! : throw new InvalidOperationException();
    }
}
