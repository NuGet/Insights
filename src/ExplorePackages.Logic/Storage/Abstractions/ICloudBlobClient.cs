namespace Knapcode.ExplorePackages
{
    public interface ICloudBlobClient
    {
        ICloudBlobContainer GetContainerReference(string containerName);
    }
}
