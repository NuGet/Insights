namespace Knapcode.ExplorePackages
{
    public interface ICloudTableClient
    {
        ICloudTable GetTableReference(string tableName);
    }
}
