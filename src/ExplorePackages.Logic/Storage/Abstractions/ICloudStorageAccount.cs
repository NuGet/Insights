namespace Knapcode.ExplorePackages
{
    public interface ICloudStorageAccount
    {
        ICloudTableClient CreateCloudTableClient();
        ICloudBlobClient CreateCloudBlobClient();
    }
}
