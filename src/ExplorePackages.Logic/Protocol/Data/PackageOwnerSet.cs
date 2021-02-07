using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public class PackageOwnerSet : IAsyncDisposable, IAsOfData
    {
        public PackageOwnerSet(DateTimeOffset asOfTimestamp, string url, string etag, IAsyncEnumerable<PackageOwner> owners)
        {
            AsOfTimestamp = asOfTimestamp;
            Url = url;
            ETag = etag;
            Owners = owners;
        }

        public DateTimeOffset AsOfTimestamp { get; }
        public string Url { get; }
        public string ETag { get; }
        public IAsyncEnumerable<PackageOwner> Owners { get; }

        public ValueTask DisposeAsync()
        {
            return Owners?.GetAsyncEnumerator().DisposeAsync() ?? default;
        }
    }
}
