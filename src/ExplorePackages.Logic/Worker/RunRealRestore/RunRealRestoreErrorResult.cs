using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class RunRealRestoreErrorResult
    {
        public RealRestoreResult Result { get; set; }
        public List<CommandResult> CommandResults { get; set; }
    }
}
