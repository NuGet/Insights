namespace NuGet.Insights.Worker
{
    public interface ITaskStateMessage
    {
        TaskStateKey TaskStateKey { get; }
        int AttemptCount { get; set; }
    }
}
