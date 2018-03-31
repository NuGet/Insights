namespace Knapcode.ExplorePackages.Website.Models
{
    public class ExploreViewModel
    {
        public ExploreViewModel()
        {
            Id = null;
            Version = null;
        }

        public ExploreViewModel(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; }
        public string Version { get; }
        public bool ImmediatelyStart => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Version);
    }
}
