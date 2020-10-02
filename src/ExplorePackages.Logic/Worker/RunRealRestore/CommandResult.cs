using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class CommandResult
    {
        public CommandResult(
            DateTimeOffset startTimestamp,
            DateTimeOffset endTimestamp,
            string fileName,
            IReadOnlyList<string> arguments,
            int? exitCode,
            bool timeout,
            string output)
        {
            StartTimestamp = startTimestamp;
            EndTimestamp = endTimestamp;
            FileName = fileName;
            Arguments = arguments;
            ExitCode = exitCode;
            Timeout = timeout;
            Output = output;
        }

        public DateTimeOffset StartTimestamp { get; }
        public DateTimeOffset EndTimestamp { get; }
        public string FileName { get; }
        public IReadOnlyList<string> Arguments { get; }
        public int? ExitCode { get; }
        public bool Timeout { get; }
        public string Output { get; }
        public bool Succeeded => ExitCode == 0 && !Timeout;
    }
}
