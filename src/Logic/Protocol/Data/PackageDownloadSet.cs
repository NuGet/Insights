using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class PackageDownloadSet : IAsyncDisposable, IAsOfData
    {
        public PackageDownloadSet(DateTimeOffset asOfTimestamp, string url, string etag, IAsyncEnumerable<PackageDownloads> downloads)
        {
            AsOfTimestamp = asOfTimestamp;
            Url = url;
            ETag = etag;
            Downloads = downloads;
        }

        public DateTimeOffset AsOfTimestamp { get; }
        public string Url { get; }
        public string ETag { get; }
        public IAsyncEnumerable<PackageDownloads> Downloads { get; }

        public ValueTask DisposeAsync()
        {
            return Downloads?.GetAsyncEnumerator().DisposeAsync() ?? default;
        }
    }
}
