using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages
{
    public class CloudTableClientWrapper : ICloudTableClient
    {
        private readonly CloudTableClient _inner;

        public CloudTableClientWrapper(CloudTableClient inner)
        {
            _inner = inner;
        }

        public ICloudTable GetTableReference(string tableName)
        {
            return new CloudTableWrapper(_inner.GetTableReference(tableName));
        }
    }
}
