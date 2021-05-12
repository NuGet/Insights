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
                var accountName = Environment.GetEnvironmentVariable("EXPLOREPACKAGES_STORAGEACCOUNTNAME");
                var sas = Environment.GetEnvironmentVariable("EXPLOREPACKAGES_STORAGESAS");
                if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(sas))
                {
                    return StorageEmulatorConnectionString;
                }

                return $"AccountName={accountName};SharedAccessSignature={sas}";
            }
        }

        public static string StorageBlobReadSharedAccessSignature
        {
            get
            {
                var sas = Environment.GetEnvironmentVariable("EXPLOREPACKAGES_STORAGEBLOBREADSAS");
                if (string.IsNullOrWhiteSpace(sas))
                {
                    return null;
                }

                return sas;
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
