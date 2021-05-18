// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class StorageId
    {
        private readonly string _value;

        public StorageId(string sortable, string unique)
        {
            Sortable = sortable;
            Unique = unique;
            _value = sortable + "-" + unique;
        }

        public string Sortable { get; }
        public string Unique { get; }

        public override string ToString()
        {
            return _value;
        }
    }
}
