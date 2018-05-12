using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDownloadSet : IDisposable
    {
        public PackageDownloadSet(string etag, IAsyncEnumerator<PackageDownloads> downloads)
        {
            ETag = etag;
            Downloads = downloads;
        }

        public string ETag { get; }
        public IAsyncEnumerator<PackageDownloads> Downloads { get; }

        public void Dispose()
        {
            Downloads?.Dispose();
        }
    }
}
