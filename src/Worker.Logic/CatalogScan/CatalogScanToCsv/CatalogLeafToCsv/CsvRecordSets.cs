// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;

namespace NuGet.Insights.Worker
{
    public class CsvRecordSets : IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>>
    {
        private readonly IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>> _sets;

        public CsvRecordSets(params IReadOnlyList<ICsvRecordSet<ICsvRecord>>[] sets)
        {
            _sets = sets;
        }

        public IReadOnlyList<ICsvRecordSet<ICsvRecord>> this[int index] => _sets[index];
        public int Count => _sets.Count;

        public IEnumerator<IReadOnlyList<ICsvRecordSet<ICsvRecord>>> GetEnumerator()
        {
            return _sets.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _sets.GetEnumerator();
        }
    }

    public class CsvRecordSets<T> : CsvRecordSets where T : class, ICsvRecord
    {
        public CsvRecordSets(CsvRecordSet<T> set1) : base(new[] { set1 })
        {
            Sets1 = new[] { set1 };
        }

        public CsvRecordSets(IReadOnlyList<CsvRecordSet<T>> sets1) : base(sets1)
        {
            Sets1 = sets1;
        }

        public IReadOnlyList<CsvRecordSet<T>> Sets1 { get; }
    }

    public class CsvRecordSets<T1, T2> : CsvRecordSets
         where T1 : class, ICsvRecord
         where T2 : class, ICsvRecord
    {
        public CsvRecordSets(CsvRecordSet<T1> set1, CsvRecordSet<T2> set2) : base(new[] { set1 }, new[] { set2 })
        {
            Sets1 = new[] { set1 };
            Sets2 = new[] { set2 };
        }

        public CsvRecordSets(IReadOnlyList<CsvRecordSet<T1>> sets1, IReadOnlyList<CsvRecordSet<T2>> sets2) : base(sets1, sets2)
        {
            Sets1 = sets1;
            Sets2 = sets2;
        }

        public IReadOnlyList<CsvRecordSet<T1>> Sets1 { get; }
        public IReadOnlyList<CsvRecordSet<T2>> Sets2 { get; }
    }

    public class CsvRecordSets<T1, T2, T3> : CsvRecordSets
         where T1 : class, ICsvRecord
         where T2 : class, ICsvRecord
         where T3 : class, ICsvRecord
    {
        public CsvRecordSets(CsvRecordSet<T1> set1, CsvRecordSet<T2> set2, CsvRecordSet<T3> set3) : base(new[] { set1 }, new[] { set2 }, new[] { set3 })
        {
            Sets1 = new[] { set1 };
            Sets2 = new[] { set2 };
            Sets3 = new[] { set3 };
        }

        public CsvRecordSets(IReadOnlyList<CsvRecordSet<T1>> sets1, IReadOnlyList<CsvRecordSet<T2>> sets2, IReadOnlyList<CsvRecordSet<T3>> sets3) : base(sets1, sets2, sets3)
        {
            Sets1 = sets1;
            Sets2 = sets2;
            Sets3 = sets3;
        }

        public IReadOnlyList<CsvRecordSet<T1>> Sets1 { get; }
        public IReadOnlyList<CsvRecordSet<T2>> Sets2 { get; }
        public IReadOnlyList<CsvRecordSet<T3>> Sets3 { get; }
    }
}
