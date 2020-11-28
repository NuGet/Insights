namespace Knapcode.ExplorePackages
{
    public interface IServiceClientFactory
    {
        ICloudStorageAccount GetAbstractedStorageAccount();
    }
}