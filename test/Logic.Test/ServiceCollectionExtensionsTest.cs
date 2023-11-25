// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NuGet.Insights
{
    public class ServiceCollectionExtensionsTest
    {
        [Fact]
        public void DoesNotAddTypesASecondTime()
        {
            // Arrange
            var builder = new ServiceCollection();
            builder.AddNuGetInsights();
            Assert.Contains(builder, x => x.ServiceType == typeof(FileDownloader));
            builder.RemoveAll<FileDownloader>();

            // Act
            builder.AddNuGetInsights();

            // Assert
            Assert.DoesNotContain(builder, x => x.ServiceType == typeof(FileDownloader));
        }

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
