// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Insights
{
    public class ServiceCollectionExtensionsTest
    {
        public class IcuMode
        {
            [OSPlatformFact(OSPlatformType.Windows)]
            public void CanDetectNotIcuMode()
            {
                Assert.False(ServiceCollectionExtensions.IcuMode());
            }

            [OSPlatformFact(OSPlatformType.Linux | OSPlatformType.OSX | OSPlatformType.FreeBSD)]
            public void CanDetectIcuMode()
            {
                Assert.True(ServiceCollectionExtensions.IcuMode());
            }
        }

        public class HasNlsInvariantBehavior
        {
            [OSPlatformFact(OSPlatformType.Windows)]
            public void CanDetectNlsInvariantMode()
            {
                Assert.True(ServiceCollectionExtensions.HasNlsInvariantBehavior());
            }

            [OSPlatformFact(OSPlatformType.Linux | OSPlatformType.OSX | OSPlatformType.FreeBSD)]
            public void CanDetectNonNlsInvariantMode()
            {
                Assert.False(ServiceCollectionExtensions.HasNlsInvariantBehavior());
            }
        }
    }
}
