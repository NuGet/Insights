namespace Knapcode.ExplorePackages.Worker
{
    public interface ILoopingMessage : ITaskStateMessage
    {
        bool Loop { get; set; }
    }
}
