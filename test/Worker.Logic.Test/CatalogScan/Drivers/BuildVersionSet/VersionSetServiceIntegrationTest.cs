// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class VersionSetServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public void ScopesReturnSameInstance()
        {
            using (var scopeA = Host.Services.CreateScope())
            using (var scopeB = Host.Services.CreateScope())
            {
                Assert.Same(
                    scopeA.ServiceProvider.GetRequiredService<VersionSetService>(),
                    scopeB.ServiceProvider.GetRequiredService<VersionSetService>());
            }
        }

        [Fact]
        public void DependencyInjectionReturnsASingletonForImplementation()
        {
            Assert.Same(
                Host.Services.GetRequiredService<VersionSetService>(),
                Host.Services.GetRequiredService<VersionSetService>());
        }

        [Fact]
        public void DependencyInjectionReturnsASingletonForInterface()
        {
            Assert.Same(
                Host.Services.GetRequiredService<IVersionSetProvider>(),
                Host.Services.GetRequiredService<IVersionSetProvider>());
        }

        [Fact]
        public async Task ReturnsDifferentInstanceWhenAnotherHandleIsActive()
        {
            // Arrange
            await Target.InitializeAsync();
            var commitTimestamp = new DateTimeOffset(2023, 4, 1, 12, 0, 0, TimeSpan.Zero);
            await Target.UpdateAsync(commitTimestamp, new());

            // Act
            using (var handleA = await Target.GetAsync())
            using (var handleB = await Target.GetAsync())
            {
                // Assert
                Assert.NotSame(handleA.Value, handleB.Value);
            }
        }

        [Fact]
        public async Task UncheckedIdsAreTrackedSeparately()
        {
            // Arrange
            await Target.InitializeAsync();
            var commitTimestamp = new DateTimeOffset(2023, 4, 1, 12, 0, 0, TimeSpan.Zero);
            await Target.UpdateAsync(
                commitTimestamp,
                new CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>
                {
                    { "Knapcode.TorSharp", new CaseInsensitiveSortedDictionary<bool> { { "1.0.0", false } } },
                    { "Newtonsoft.Json", new CaseInsensitiveSortedDictionary<bool> { { "9.0.1", false } } },
                });

            // Act
            using (var handleA = await Target.GetAsync())
            using (var handleB = await Target.GetAsync())
            using (var handleC = await Target.GetAsync())
            {
                handleA.Value.TryGetId("newtonsoft.json", out _);
                handleB.Value.TryGetId("knapcode.torsharp", out _);

                // Assert
                Assert.Equal(new[] { "Knapcode.TorSharp" }, handleA.Value.GetUncheckedIds());
                Assert.Equal(new[] { "Newtonsoft.Json" }, handleB.Value.GetUncheckedIds());
                Assert.Equal(new[] { "Knapcode.TorSharp", "Newtonsoft.Json" }, handleC.Value.GetUncheckedIds().OrderBy(x => x, StringComparer.Ordinal));
            }
        }

        [Fact]
        public async Task ReturnsStaleVersionSetWhenAnotherHandleIsActive()
        {
            // Arrange
            await Target.InitializeAsync();
            var commitTimestamp = new DateTimeOffset(2023, 4, 1, 12, 0, 0, TimeSpan.Zero);
            await Target.UpdateAsync(commitTimestamp, new());

            // Act
            using (var handleA = await Target.GetAsync())
            {
                await Target.UpdateAsync(
                    commitTimestamp,
                    new CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>
                    {
                        {
                            "Newtonsoft.Json",
                            new CaseInsensitiveSortedDictionary<bool>
                            {
                                { "9.0.1", false }
                            }
                        }
                    });

                using var handleB = await Target.GetAsync();

                // Assert
                Assert.Empty(handleA.Value.GetUncheckedIds());
                Assert.Empty(handleB.Value.GetUncheckedIds());
            }
        }

        [Fact]
        public async Task ReturnsNewVersionSetWhenAnotherHandleIsReleased()
        {
            // Arrange
            await Target.InitializeAsync();
            var commitTimestamp = new DateTimeOffset(2023, 4, 1, 12, 0, 0, TimeSpan.Zero);
            await Target.UpdateAsync(commitTimestamp, new());

            // Act
            IVersionSet versionSetA;
            using (var handle = await Target.GetAsync())
            {
                versionSetA = handle.Value;
            }

            IVersionSet versionSetB;
            using (var handle = await Target.GetAsync())
            {
                versionSetB = handle.Value;
            }

            // Assert
            Assert.NotSame(versionSetA, versionSetB);
        }

        [Fact]
        public async Task ReturnsFreshDataWhenAnotherHandleIsReleased()
        {
            // Arrange
            await Target.InitializeAsync();
            var commitTimestamp = new DateTimeOffset(2023, 4, 1, 12, 0, 0, TimeSpan.Zero);
            await Target.UpdateAsync(commitTimestamp, new());

            // Act
            IVersionSet versionSetA;
            using (var handle = await Target.GetAsync())
            {
                versionSetA = handle.Value;
            }

            await Target.UpdateAsync(
                commitTimestamp,
                new CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>
                {
                    {
                        "Newtonsoft.Json",
                        new CaseInsensitiveSortedDictionary<bool>
                        {
                            { "9.0.1", false }
                        }
                    }
                });

            IVersionSet versionSetB;
            using (var handle = await Target.GetAsync())
            {
                versionSetB = handle.Value;
            }

            // Assert
            Assert.Empty(versionSetA.GetUncheckedIds());
            Assert.Equal(new[] { "Newtonsoft.Json" }, versionSetB.GetUncheckedIds());
        }

        public VersionSetService Target => Host.Services.GetRequiredService<VersionSetService>();

        public VersionSetServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
