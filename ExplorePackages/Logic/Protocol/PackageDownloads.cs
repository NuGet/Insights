namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDownloads
    {
        public PackageDownloads(string id, string version, long downloads)
        {
            Id = id;
            Version = version;
            Downloads = downloads;
        }

        public string Id { get; }
        public string Version { get; }
        public long Downloads { get; }
    }
}
