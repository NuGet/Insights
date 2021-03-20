using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.VersionSets
{
    public class CaseInsensitiveDictionary<TValue> : Dictionary<string, TValue>
    {
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
