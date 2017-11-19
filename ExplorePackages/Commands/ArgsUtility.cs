using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Commands
{
    public static class ArgsUtility
    {
        public static bool HasArg(List<string> args, string arg)
        {
            var hasArg = false;
            for (var i = 0; i < args.Count; i++)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(args[i], arg))
                {
                    hasArg = true;
                    args.RemoveAt(i);
                    i--;
                }
            }

            return hasArg;
        }
    }
}
