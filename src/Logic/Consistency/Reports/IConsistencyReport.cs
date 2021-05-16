namespace NuGet.Insights
{
    public interface IConsistencyReport
    {
        bool IsConsistent { get; }
    }
}
