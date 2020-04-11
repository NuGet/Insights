using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDownloadSet : IAsyncDisposable
    {
        public PackageDownloadSet(string etag, IAsyncEnumerator<PackageDownloads> downloads)
        {
            ETag = etag;
            Downloads = downloads;
        }

        public string ETag { get; }
        public IAsyncEnumerator<PackageDownloads> Downloads { get; }

        public ValueTask DisposeAsync()
        {
            return Downloads?.DisposeAsync() ?? default;
        }
    }
}
