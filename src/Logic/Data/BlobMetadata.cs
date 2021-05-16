namespace NuGet.Insights
{
    public class BlobMetadata
    {
        public BlobMetadata(bool exists, bool hasContentMD5Header, string contentMD5)
        {
            Exists = exists;
            HasContentMD5Header = hasContentMD5Header;
            ContentMD5 = contentMD5;
        }

        public bool Exists { get; }
        public bool HasContentMD5Header { get; }
        public string ContentMD5 { get; }
    }
}
