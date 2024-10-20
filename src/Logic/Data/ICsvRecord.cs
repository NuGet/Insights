// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public interface ICsvRecord
    {
        void Write(List<string> fields);
        void Write(TextWriter writer);
        Task WriteAsync(TextWriter writer);

        /// <summary>
        /// Set all null properties on the CSV record to an empty string. This is done as an initialization step prior
        /// to serialization. A null string an empty string are indistinguishable in CSV without some magic value or
        /// specialized serialization. Therefore, we simply set all null strings to empty string to keep it simple. This
        /// also allows value comparison (implemented by records) to work effectively when comparing initialized records
        /// against deserialized ones.
        /// </summary>
        void SetEmptyStrings();
    }

    public interface ICsvRecord<T> : ICsvRecord, IEquatable<T> where T : ICsvRecord
    {
        static abstract int FieldCount { get; }
        static abstract void WriteHeader(TextWriter writer);

        static abstract T ReadNew(Func<string> getNextField);
    }
}
