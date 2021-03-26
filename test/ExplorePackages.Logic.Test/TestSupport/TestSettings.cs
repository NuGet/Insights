using System;
using System.Text.RegularExpressions;
using Xunit;

namespace Knapcode.ExplorePackages
{
    public static class TestSettings
    {
        private const string StorageEmulatorConnectionString = StorageUtility.EmulatorConnectionString;

        public static bool IsStorageEmulator => StorageConnectionString == StorageEmulatorConnectionString;

        public static string StorageConnectionString
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("EXPLOREPACKAGES_STORAGECONNECTIONSTRING");
                if (string.IsNullOrWhiteSpace(env))
                {
                    return StorageEmulatorConnectionString;
                }

                return env;
            }
        }

        public static readonly Regex StoragePrefixPattern = new Regex(@"t(?<Date>\d{6})[a-z234567]{10}");

        public static string NewStoragePrefix()
        {
            var randomBytes = new byte[6];
            ThreadLocalRandom.NextBytes(randomBytes);
            var storagePrefix = "t" + DateTimeOffset.UtcNow.ToString("yyMMdd") + randomBytes.ToTrimmedBase32();
            Assert.Matches(StoragePrefixPattern, storagePrefix);
            return storagePrefix;
        }
    }
}
