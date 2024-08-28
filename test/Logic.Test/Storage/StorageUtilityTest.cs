// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class StorageUtilityTest
    {
        [Theory]
        [InlineData(1000, "newtonsoft.json", 892)]
        [InlineData(1000, "awssdk.translate", 892)]
        [InlineData(100, "newtonsoft.json", 92)]
        [InlineData(100, "Newtonsoft.Json", 96)]
        [InlineData(1000, "microsoft.extensions.logging.abstractions/3.0.0-preview9.19423.4", 938)]
        [InlineData(1000, "microsoft.extensions.logging.abstractions/9.0.0-preview.1.24080.9", 557)]
        public void GetBucket_ReturnsExpectedValue(int bucketCount, string bucketKey, int bucket)
        {
            Assert.Equal(bucket, StorageUtility.GetBucket(bucketCount, bucketKey));
        }

        [Theory]
        [InlineData(1000, "newtonsoft.json", "", 892)]
        [InlineData(1000, "microsoft.extensions.logging.abstractions/3.0.0-preview9.19423.4", "", 938)]
        [InlineData(1000, "microsoft.extensions.logging.abstractions/3.0.0-preview9.19423.4", "00", 387)]
        [InlineData(1000, "microsoft.extensions.logging.abstractions/9.0.0-preview.1.24080.9", "", 557)]
        [InlineData(1000, "microsoft.extensions.logging.abstractions/9.0.0-preview.1.24080.9", "00", 857)]
        [InlineData(1000, "awssdk.translate", "", 892)]
        [InlineData(1000, "newtonsoft.json", "00", 546)]
        [InlineData(1000, "newtonsoft.json", "01", 350)]
        [InlineData(100, "newtonsoft.json", "00", 46)]
        public void GetBucketWithSuffix_ReturnsExpectedValue(int bucketCount, string bucketKey, string suffixHex, int bucket)
        {
            var suffix = Convert.FromHexString(suffixHex);
            Assert.Equal(bucket, StorageUtility.GetBucket(bucketCount, bucketKey, suffix));
        }
    }
}
