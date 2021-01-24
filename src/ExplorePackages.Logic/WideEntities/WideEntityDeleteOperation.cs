namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntityDeleteOperation : WideEntityOperation
    {
        public WideEntityDeleteOperation(WideEntity existing)
        {
            Existing = existing;
        }

        public WideEntity Existing { get; }
    }
}
