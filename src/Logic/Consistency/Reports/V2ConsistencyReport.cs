namespace NuGet.Insights
{
    public class V2ConsistencyReport : IConsistencyReport
    {
        public V2ConsistencyReport(bool isConsistent, bool hasPackage)
        {
            IsConsistent = isConsistent;
            HasPackage = hasPackage;
        }

        public bool IsConsistent { get; }
        public bool HasPackage { get; }
    }
}
