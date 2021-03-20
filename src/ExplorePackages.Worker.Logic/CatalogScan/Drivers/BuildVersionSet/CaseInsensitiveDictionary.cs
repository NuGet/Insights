using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public class CaseInsensitiveDictionary<TValue> : Dictionary<string, TValue>
    {
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
