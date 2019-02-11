namespace Knapcode.ExplorePackages.Entities
{
    public class CommitCollectorProgressTokenEntity
    {
        public long CommitCollectorProgressTokenKey { get; set; }
        public string Name { get; set; }
        public long FirstCommitTimestamp { get; set; }
        public long LastCommitTimestamp { get; set; }
        public string SerializedProgressToken { get; set; }
    }
}
