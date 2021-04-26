using System;

namespace Knapcode.ExplorePackages.TablePrefixScan
{
    public abstract class TablePrefixScanStep
    {
        protected TablePrefixScanStep(TableQueryParameters parameters, int depth)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Depth = depth;
        }

        public TableQueryParameters Parameters { get; }
        public int Depth { get; }
        public abstract string DebuggerDisplay { get; }

        public override string ToString()
        {
            return DebuggerDisplay;
        }
    }
}
