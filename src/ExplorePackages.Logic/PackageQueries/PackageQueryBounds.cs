using System;
using System.Collections.Generic;
using System.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryBounds
    {
        public PackageQueryBounds(
            IReadOnlyDictionary<string, string> queryNameToCursorName,
            Dictionary<string, DateTimeOffset> cursorNameToStart,
            DateTimeOffset end)
        {
            QueryNameToCursorName = queryNameToCursorName;
            CursorNameToStart = cursorNameToStart;
            Start = cursorNameToStart.Values.Min();
            End = end;
        }

        public IReadOnlyDictionary<string, string> QueryNameToCursorName { get; }
        public Dictionary<string, DateTimeOffset> CursorNameToStart { get; }
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; }
    }
}
