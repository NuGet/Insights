// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;

#nullable enable

namespace NuGet.Insights
{
    public class EquatableList<T> : IReadOnlyList<T>, IEquatable<EquatableList<T>>
    {
        private IReadOnlyList<T> _items;
        private readonly int _hashCode;

        public EquatableList(IEnumerable<T> items)
        {
            _items = items.ToList();
            var hashCode = new HashCode();
            foreach (var item in _items)
            {
                hashCode.Add(item);
            }
            _hashCode = hashCode.ToHashCode();
        }

        public T this[int index] => _items[index];
        public int Count => _items.Count;

        public bool Equals(EquatableList<T>? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_hashCode != other._hashCode || _items.Count != other._items.Count)
            {
                return false;
            }

            for (var i = 0; i < _items.Count; i++)
            {
                if (!Equals(_items[i], other._items[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as EquatableList<T>);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }
}
