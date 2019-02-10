namespace Knapcode.ExplorePackages.Entities
{
    public class CommitCollectorSequentialProgressEntity
    {
        public long CommitCollectorSequentialProgressKey { get; set; }
        public string Name { get; set; }
        public long FirstCommitTimestamp { get; set; }
        public long LastCommitTimestamp { get; set; }
        public int Skip { get; set; }
    }
}
