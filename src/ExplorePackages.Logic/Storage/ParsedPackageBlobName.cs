namespace Knapcode.ExplorePackages
{
    public class ParsedPackageBlobName
    {
        public ParsedPackageBlobName(string original, string canonical, string id, string version, FileArtifactType type)
        {
            Original = original;
            Canonical = canonical;
            IsCanonical = Original == Canonical;
            Id = id;
            Version = version;
            Type = type;
        }

        public string Original { get; }
        public string Canonical { get; }
        public bool IsCanonical { get; }
        public string Id { get; }
        public string Version { get; }
        public FileArtifactType Type { get; }
    }
}
