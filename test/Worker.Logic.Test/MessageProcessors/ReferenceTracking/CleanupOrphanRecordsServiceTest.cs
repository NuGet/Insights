// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.ReferenceTracking;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsServiceTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task NoDeletions()
        {
            // Arrange
            var subjectRecords = Enumerable
                .Range(0, 10)
                .Select(x => new TestSubjectRecord { BucketKey = x.ToString(CultureInfo.InvariantCulture), Id = "Subject" + x })
                .ToList();

            // Act
            await SetReferencesAsync(subjectRecords, new Dictionary<string, List<int>>
            {
                { "Owner0", new List<int> { 0, 1, 2 } },
                { "Owner1", new List<int> { 3, 4, 5 } },
                { "Owner2", new List<int> { 6, 7, 8, 9 } },
            });
            await InitializeCsvBlobs(subjectRecords);
            await SetReferencesAsync(subjectRecords, new Dictionary<string, List<int>>
            {
                { "Owner0", new List<int> { 0, 1, 2, 3 } },
                { "Owner1", new List<int> { 3, 4, 5, 6 } },
                { "Owner2", new List<int> { 6, 7, 8, 9 } },
            });

            // Assert
            await Verify(
                await Task.WhenAll(
                    GetOwnerToSubjectAsync(step: 1),
                    GetSubjectToOwnerAsync(step: 1),
                    GetCsvAsync(bucket: 0, step: 1),
                    GetCsvAsync(bucket: 1, step: 1),
                    GetCsvAsync(bucket: 2, step: 1)));

            // Act
            await CleanupOrphanRecordsAsync();

            // Assert
            await Verify(
                await Task.WhenAll(
                    GetOwnerToSubjectAsync(step: 1), // This file is unchanged.
                    GetSubjectToOwnerAsync(step: 1), // This file is unchanged.
                    GetCsvAsync(bucket: 0, step: 1), // This file is unchanged.
                    GetCsvAsync(bucket: 1, step: 1), // This file is unchanged.
                    GetCsvAsync(bucket: 2, step: 1))) // This file is unchanged.
                .DisableRequireUniquePrefix();
        }

        [Fact]
        public async Task NoOrphans()
        {
            // Arrange
            var subjectRecords = Enumerable
                .Range(0, 10)
                .Select(x => new TestSubjectRecord { BucketKey = x.ToString(CultureInfo.InvariantCulture), Id = "Subject" + x })
                .ToList();

            // Act
            await SetReferencesAsync(subjectRecords, new Dictionary<string, List<int>>
            {
                { "Owner0", new List<int> { 0, 1, 2, 3 } },
                { "Owner1", new List<int> { 3, 4, 5, 6 } },
                { "Owner2", new List<int> { 6, 7, 8, 9 } },
            });
            await InitializeCsvBlobs(subjectRecords);
            await SetReferencesAsync(subjectRecords, new Dictionary<string, List<int>>
            {
                { "Owner0", new List<int> { 0, 1, 2 } },
                { "Owner1", new List<int> { 3, 4, 5 } },
                { "Owner2", new List<int> { 6, 7, 8, 9 } },
            });

            // Assert
            await Verify(
                await Task.WhenAll(
                    GetOwnerToSubjectAsync(step: 1),
                    GetSubjectToOwnerAsync(step: 1),
                    GetCsvAsync(bucket: 0, step: 1),
                    GetCsvAsync(bucket: 1, step: 1),
                    GetCsvAsync(bucket: 2, step: 1)));

            // Act
            await CleanupOrphanRecordsAsync();

            // Assert
            await Verify(
                await Task.WhenAll(
                    GetOwnerToSubjectAsync(step: 1), // This file is unchanged.
                    GetSubjectToOwnerAsync(step: 2),
                    GetCsvAsync(bucket: 0, step: 1), // This file is unchanged.
                    GetCsvAsync(bucket: 1, step: 1), // This file is unchanged.
                    GetCsvAsync(bucket: 2, step: 1))) // This file is unchanged.
                .DisableRequireUniquePrefix(); 
        }

        [Fact]
        public async Task ClearedCsv()
        {
            // Arrange
            var subjectRecords = Enumerable
                .Range(0, 10)
                .Select(x => new TestSubjectRecord { BucketKey = x.ToString(CultureInfo.InvariantCulture), Id = "Subject" + x })
                .ToList();

            // Act
            await SetReferencesAsync(subjectRecords, new Dictionary<string, List<int>>
            {
                { "Owner0", new List<int> { 0, 1, 2, 3 } },
                { "Owner1", new List<int> { 3, 4, 5, 6 } },
                { "Owner2", new List<int> { 6, 7, 8, 9 } },
            });
            await InitializeCsvBlobs(subjectRecords);
            await SetReferencesAsync(subjectRecords, new Dictionary<string, List<int>>
            {
                // No changes are made to Owner1's references.
                { "Owner0", new List<int> { 0, 1, 2, 3 } },

                // Owner1 has its references updated, causing Subject 5 to be marked as a candidate orphan.
                // It is an actual orphan because Owner1 was the only one referencing it.
                { "Owner1", new List<int> { 3, 4, 6 } },

                // Owner2 has its references cleared, causing Subject 6, 7, 8, and 9 to be marked as candidate orphans.
                // Subject 6 is still referenced by Owner 1 so it's not an actual orphan but 7, 8, and 9 are.
                { "Owner2", new List<int>() },
            });

            // Assert
            await Verify(
                await Task.WhenAll(
                    GetOwnerToSubjectAsync(step: 1),
                    GetSubjectToOwnerAsync(step: 1),
                    GetCsvAsync(bucket: 0, step: 1),
                    GetCsvAsync(bucket: 1, step: 1),
                    GetCsvAsync(bucket: 2, step: 1)));

            // Act
            await CleanupOrphanRecordsAsync();

            // Assert
            await Verify(
                await Task.WhenAll(
                    GetOwnerToSubjectAsync(step: 1), // This file is unchanged.
                    GetSubjectToOwnerAsync(step: 2),
                    GetCsvAsync(bucket: 0, step: 2),
                    GetCsvAsync(bucket: 1, step: 1), // This file is unchanged.
                    GetCsvAsync(bucket: 2, step: 2)))
                .DisableRequireUniquePrefix();
        }
        private async Task CleanupOrphanRecordsAsync()
        {
            await Target.InitializeAsync();
            Assert.True(await Target.StartAsync());
            await ProcessQueueAsync(async () => !await Target.IsRunningAsync());
        }

        private async Task SetReferencesAsync(List<TestSubjectRecord> subjectRecords, Dictionary<string, List<int>> ownerPartitionKeyToSubjectIndex)
        {
            await ReferenceTracker.InitializeAsync(OwnerToSubjectTableName, SubjectToOwnerTableName);
            foreach ((var ownerPartitionKey, var subjectIndexes) in ownerPartitionKeyToSubjectIndex)
            {
                await ReferenceTracker.SetReferencesAsync(
                    OwnerToSubjectTableName,
                    SubjectToOwnerTableName,
                    Adapter.OwnerType,
                    Adapter.SubjectType,
                    ownerPartitionKey,
                    new Dictionary<string, IReadOnlySet<SubjectEdge>>
                    {
                        {
                            string.Empty,
                            subjectIndexes
                                .Select(x => new SubjectEdge(
                                    subjectRecords[x].BucketKey,
                                    subjectRecords[x].Id,
                                    Encoding.UTF8.GetBytes(subjectRecords[x].Id)))
                                .ToHashSet()
                        }
                    });
            }
        }

        private async Task InitializeCsvBlobs(List<TestSubjectRecord> subjectRecords)
        {
            await AppendResultStorageService.InitializeAsync(Options.Value.CsvRecordTableName, CsvResultStorage.ResultContainerName);
            await AppendResultStorageService.AppendAsync(
                Options.Value.CsvRecordTableName,
                Options.Value.AppendResultStorageBucketCount,
                subjectRecords.Select(x => new CsvRecordSet<TestSubjectRecord>(x.BucketKey, new[] { x })).ToList());
            var buckets = await AppendResultStorageService.GetAppendedBucketsAsync(Options.Value.CsvRecordTableName);
            foreach (var bucket in buckets)
            {
                await AppendResultStorageService.CompactAsync<TestSubjectRecord>(
                    Options.Value.CsvRecordTableName,
                    CsvResultStorage.ResultContainerName,
                    bucket,
                    force: false,
                    CsvResultStorage.Prune);
            }
            await AppendResultStorageService.DeleteAsync(Options.Value.CsvRecordTableName);
        }

        public string ResultContainerName { get; }
        public string OwnerToSubjectTableName { get; }
        public string SubjectToOwnerTableName { get; }
        public ReferenceTracker ReferenceTracker => Host.Services.GetRequiredService<ReferenceTracker>();
        public CsvTemporaryStorageFactory CsvTemporaryStorageFactory => Host.Services.GetRequiredService<CsvTemporaryStorageFactory>();
        public ICsvResultStorage<TestSubjectRecord> CsvResultStorage => Host.Services.GetRequiredService<ICsvResultStorage<TestSubjectRecord>>();
        public AppendResultStorageService AppendResultStorageService => Host.Services.GetRequiredService<AppendResultStorageService>();
        public ICleanupOrphanRecordsAdapter<TestSubjectRecord> Adapter => Host.Services.GetRequiredService<ICleanupOrphanRecordsAdapter<TestSubjectRecord>>();
        public ICleanupOrphanRecordsService<TestSubjectRecord> Target => Host.Services.GetRequiredService<ICleanupOrphanRecordsService<TestSubjectRecord>>();

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddSingleton(this);
                serviceCollection.AddSingleton(x => SchemaCollectionBuilder
                    .Default
                    .Add(new SchemaV1<CsvCompactMessage<TestSubjectRecord>>("cc.ts"))
                    .Add(new SchemaV1<CleanupOrphanRecordsMessage<TestSubjectRecord>>("cor.ts"))
                    .Build());
                serviceCollection.AddCleanupOrphanRecordsService<TestCleanupOrphanRecordsAdapter, TestSubjectRecord>();
                serviceCollection.AddTransient<ICsvResultStorage<TestSubjectRecord>, TestSubjectRecordStorage>();
                serviceCollection.AddTransient<IMessageProcessor<CsvCompactMessage<TestSubjectRecord>>, CsvCompactorProcessor<TestSubjectRecord>>();
                serviceCollection.AddTransient<ICsvRecordStorage>(x =>
                {
                    var resultStorage = x.GetRequiredService<ICsvResultStorage<TestSubjectRecord>>();
                    return new CsvRecordStorage(
                        resultStorage.ResultContainerName,
                        typeof(TestSubjectRecord),
                        AppendResultStorageService.CompactPrefix);
                });
            });
        }

        private async Task<Target> GetOwnerToSubjectAsync(int step)
        {
            return await GetOwnerToSubjectAsync(OwnerToSubjectTableName, x => x.ToBase64(), step);
        }

        private async Task<Target> GetSubjectToOwnerAsync(int step)
        {
            return await GetSubjectToOwnerAsync(SubjectToOwnerTableName, step);
        }

        private async Task<Target> GetCsvAsync(int bucket, int step)
        {
            return await GetCsvAsync<TestSubjectRecord>(bucket, step, tableDisplayName: "TestSubjects");
        }

        public CleanupOrphanRecordsServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            ResultContainerName = StoragePrefix + "1tsr1";
            OwnerToSubjectTableName = StoragePrefix + "1o2s1";
            SubjectToOwnerTableName = StoragePrefix + "1s2o1";
        }
    }
}
