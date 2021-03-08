using System;
using System.Collections.Generic;
using System.Text;

namespace Knapcode.ExplorePackages
{
    public static class TestSettings
    {
        public static string StorageConnectionString
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("EXPLOREPACKAGES_STORAGECONNECTIONSTRING");
                if (string.IsNullOrWhiteSpace(env))
                {
                    return "UseDevelopmentStorage=true";
                }

                return env;
            }
        }
    }
}
