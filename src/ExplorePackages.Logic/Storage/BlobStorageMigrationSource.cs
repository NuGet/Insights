namespace Knapcode.ExplorePackages.Logic
{
    public class BlobStorageMigrationSource
    {
        public BlobStorageMigrationSource(string endpointSuffix, string accountName, string sasToken, string containerName)
        {
            EndpointSuffix = endpointSuffix;
            AccountName = accountName;
            SasToken = sasToken;
            ContainerName = containerName;
        }

        public string EndpointSuffix { get; }
        public string AccountName { get; }
        public string SasToken { get; }
        public string ContainerName { get; }
    }
}
