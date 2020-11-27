using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
{
    public class RunRealRestoreErrorResult
    {
        public RealRestoreResult Result { get; set; }
        public List<CommandResult> CommandResults { get; set; }
    }
}
