using System;

namespace Knapcode.ExplorePackages
{
    public abstract class TablePrefixScanStep
    {
        protected TablePrefixScanStep(TablePrefixScanStepType type, TableQueryParameters parameters, int depth)
        {
            Type = type;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Depth = depth;
        }

        public TablePrefixScanStepType Type { get; }
        public TableQueryParameters Parameters { get; }
        public int Depth { get; }
        public abstract string DebuggerDisplay { get; }

        public override string ToString() => DebuggerDisplay;
    }
}
