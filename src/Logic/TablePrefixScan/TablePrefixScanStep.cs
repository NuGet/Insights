// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.TablePrefixScan
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
