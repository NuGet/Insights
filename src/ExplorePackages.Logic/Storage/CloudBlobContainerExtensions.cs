using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages
{
    public static class CloudBlobContainerExtensions
    {
        public static async Task<IReadOnlyList<ICloudBlob>> ListBlobsAsync(this CloudBlobContainer container, QueryLoopMetrics metrics)
        {
            using (metrics)
            {
                var blobs = new List<ICloudBlob>();
                BlobContinuationToken token = null;
                do
                {
                    BlobResultSegment segment;
                    using (metrics.TrackQuery())
                    {
                        segment = await container.ListBlobsSegmentedAsync(token);
                    }

                    blobs.AddRange(segment.Results.Cast<ICloudBlob>());
                    token = segment.ContinuationToken;
                }
                while (token != null);

                return blobs;
            }
        }
    }
}
