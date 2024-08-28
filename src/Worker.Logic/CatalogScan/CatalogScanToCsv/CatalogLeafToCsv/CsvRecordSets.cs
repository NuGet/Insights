// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;

namespace NuGet.Insights.Worker
{
    public class CsvRecordSets : IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>
    {
        private readonly IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>> _sets;

        public CsvRecordSets(params IReadOnlyList<IAggregatedCsvRecord>[] sets)
        {
            _sets = sets;
        }

        public IReadOnlyList<IAggregatedCsvRecord> this[int index] => _sets[index];
        public int Count => _sets.Count;

        public IEnumerator<IReadOnlyList<IAggregatedCsvRecord>> GetEnumerator()
        {
            return _sets.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _sets.GetEnumerator();
        }
    }

    public class CsvRecordSets<T1, T2> : CsvRecordSets
         where T1 : class, IAggregatedCsvRecord<T1>
         where T2 : class, IAggregatedCsvRecord<T2>
    {
        public CsvRecordSets(IReadOnlyList<T1> set1, IReadOnlyList<T2> set2) : base(set1, set2)
        {
            Records1 = set1;
            Records2 = set2;
        }

        public IReadOnlyList<T1> Records1 { get; }
        public IReadOnlyList<T2> Records2 { get; }
    }

    public class CsvRecordSets<T1, T2, T3> : CsvRecordSets
         where T1 : class, IAggregatedCsvRecord<T1>
         where T2 : class, IAggregatedCsvRecord<T2>
         where T3 : class, IAggregatedCsvRecord<T3>
    {
        public CsvRecordSets(IReadOnlyList<T1> set1, IReadOnlyList<T2> set2, IReadOnlyList<T3> set3) : base(set1, set2, set3)
        {
            Records1 = set1;
            Records2 = set2;
            Records3 = set3;
        }

        public IReadOnlyList<T1> Records1 { get; }
        public IReadOnlyList<T2> Records2 { get; }
        public IReadOnlyList<T3> Records3 { get; }
    }
}
