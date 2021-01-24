using System;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntityReplaceOperation : WideEntityOperation
    {
        public WideEntityReplaceOperation(WideEntity existing, ReadOnlyMemory<byte> content)
        {
            Existing = existing;
            Content = content;
        }

        public WideEntity Existing { get; }
        public ReadOnlyMemory<byte> Content { get; }
    }
}
