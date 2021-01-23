namespace Knapcode.ExplorePackages.Worker
{
    public interface ISchemaSerializer<T>
    {
        string Name { get; }
        int LatestVersion { get; }
        ISerializedEntity SerializeData(T message);
        ISerializedEntity SerializeMessage(T message);
    }

    public interface ISchemaSerializer
    {
        ISerializedEntity SerializeMessage(object message);
    }
}
