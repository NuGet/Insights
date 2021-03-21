using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages
{
    public static class CloudStorageExtensions
    {
        public static async Task<List<CloudBlobContainer>> ListContainersAsync(this CloudBlobClient client, string prefix)
        {
            BlobContinuationToken token = null;
            var output = new List<CloudBlobContainer>();
            do
            {
                var segment = await client.ListContainersSegmentedAsync(prefix, token);
                token = segment.ContinuationToken;
                foreach (var container in segment.Results)
                {
                    output.Add(container);
                }
            }
            while (token != null);

            output = output.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();

            return output;
        }

        public static async Task<List<CloudTable>> ListTablesAsync(this CloudTableClient client, string prefix)
        {
            TableContinuationToken token = null;
            var output = new List<CloudTable>();
            do
            {
                var segment = await client.ListTablesSegmentedAsync(prefix, token);
                token = segment.ContinuationToken;
                foreach (var item in segment.Results)
                {
                    output.Add(item);
                }
            }
            while (token != null);

            output = output.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();

            return output;
        }
    }
}
