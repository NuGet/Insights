using System.Collections;
using System.Collections.Generic;

namespace NuGet.Insights.Worker
{
    public class CsvRecordSets : IReadOnlyList<ICsvRecordSet<ICsvRecord>>
    {
        private readonly IReadOnlyList<ICsvRecordSet<ICsvRecord>> _sets;

        public CsvRecordSets(params ICsvRecordSet<ICsvRecord>[] sets)
        {
            _sets = sets;
        }

        public ICsvRecordSet<ICsvRecord> this[int index] => _sets[index];
        public int Count => _sets.Count;

        public IEnumerator<ICsvRecordSet<ICsvRecord>> GetEnumerator()
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
        public CsvRecordSets(CsvRecordSet<T> set1) : base(set1)
        {
            Set1 = set1;
        }

        public ICsvRecordSet<T> Set1 { get; }

        public static explicit operator CsvRecordSets<T>(CsvRecordSet<T> set1) => new CsvRecordSets<T>(set1);
    }

    public class CsvRecordSets<T1, T2> : CsvRecordSets
         where T1 : class, ICsvRecord
         where T2 : class, ICsvRecord
    {
        public CsvRecordSets(CsvRecordSet<T1> set1, CsvRecordSet<T2> set2) : base(set1, set2)
        {
            Set1 = set1;
            Set2 = set2;
        }

        public ICsvRecordSet<T1> Set1 { get; }
        public ICsvRecordSet<T2> Set2 { get; }
    }
}
