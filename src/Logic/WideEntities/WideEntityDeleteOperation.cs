namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntityDeleteOperation : WideEntityOperation
    {
        public WideEntityDeleteOperation(WideEntity existing)
            : base(existing.PartitionKey)
        {
            Existing = existing;
        }

        public WideEntity Existing { get; }
    }
}
