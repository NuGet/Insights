namespace Knapcode.ExplorePackages.Worker
{
    public interface ITaskStateMessage
    {
        TaskStateKey TaskStateKey { get; }
        int AttemptCount { get; set; }
    }
}
