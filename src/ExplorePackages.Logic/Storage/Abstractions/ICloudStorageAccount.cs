using System;
using System.Text;

namespace Knapcode.ExplorePackages
{
    public interface ICloudStorageAccount
    {
        ICloudTableClient CreateCloudTableClient();
        ICloudBlobClient CreateCloudBlobClient();
    }
}
