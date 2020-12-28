using System;

namespace Knapcode.ExplorePackages
{
    public abstract class TablePrefixScanResult
    {
        protected TablePrefixScanResult(TableQueryParameters parameters, int depth)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Depth = depth;
        }

        public TableQueryParameters Parameters { get; }
        public int Depth { get; }
        public abstract string DebuggerDisplay { get; }

        public override string ToString() => DebuggerDisplay;
    }
}
