// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.ReferenceTracking;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsServiceTest : BaseWorkerLogicIntegrationTest
    {
        public const string CleanupOrphanRecordsServiceTest_WithNoDeletionsDir = nameof(CleanupOrphanRecordsServiceTest_WithNoDeletions);
        public const string CleanupOrphanRecordsServiceTest_WithNoOrphansDir = nameof(CleanupOrphanRecordsServiceTest_WithNoOrphans);
        public const string CleanupOrphanRecordsServiceTest_WithClearedCsvDir = nameof(CleanupOrphanRecordsServiceTest_WithClearedCsv);

        [Fact]
        public async Task CleanupOrphanRecordsServiceTest_WithNoDeletions()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 3;
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
            await AssertOwnerToSubjectAsync(CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1, x => x.ToBase64());
            await AssertSubjectToOwnerAsync(CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1, 0);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1, 1);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1, 2);

            // Act
            await CleanupOrphanRecordsAsync();

            // Assert
            await AssertOwnerToSubjectAsync(CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1, x => x.ToBase64()); // This file is unchanged.
            await AssertSubjectToOwnerAsync(CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1); // This file is unchanged.
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1, 0); // This file is unchanged.
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1, 1); // This file is unchanged.
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoDeletionsDir, Step1, 2); // This file is unchanged.
        }

        [Fact]
        public async Task CleanupOrphanRecordsServiceTest_WithNoOrphans()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 3;
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
            await AssertOwnerToSubjectAsync(CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1, x => x.ToBase64());
            await AssertSubjectToOwnerAsync(CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1, 0);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1, 1);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1, 2);

            // Act
            await CleanupOrphanRecordsAsync();

            // Assert
            await AssertOwnerToSubjectAsync(CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1, x => x.ToBase64()); // This file is unchanged.
            await AssertSubjectToOwnerAsync(CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step2);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1, 0); // This file is unchanged.
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1, 1); // This file is unchanged.
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithNoOrphansDir, Step1, 2); // This file is unchanged.
        }

        [Fact]
        public async Task CleanupOrphanRecordsServiceTest_WithClearedCsv()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 3;
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
            await AssertOwnerToSubjectAsync(CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step1, x => x.ToBase64());
            await AssertSubjectToOwnerAsync(CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step1);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step1, 0);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step1, 1);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step1, 2);

            // Act
            await CleanupOrphanRecordsAsync();

            // Assert
            await AssertOwnerToSubjectAsync(CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step1, x => x.ToBase64()); // This file is unchanged.
            await AssertSubjectToOwnerAsync(CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step2);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step2, 0);
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step1, 1); // This file is unchanged.
            await AssertCsvAsync<TestSubjectRecord>(CsvResultStorage.ResultContainerName, CleanupOrphanRecordsServiceTest_WithClearedCsvDir, Step2, 2);
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
            await AppendResultStorageService.InitializeAsync(Options.Value.CsvRecordTableNamePrefix);
            await AppendResultStorageService.AppendAsync(
                Options.Value.CsvRecordTableNamePrefix,
                Options.Value.AppendResultStorageBucketCount,
                subjectRecords);
            var buckets = await AppendResultStorageService.GetAppendedBucketsAsync(Options.Value.CsvRecordTableNamePrefix);

            await (await ServiceClientFactory.GetBlobServiceClientAsync(Options.Value)).GetBlobContainerClient(CsvResultStorage.ResultContainerName).CreateIfNotExistsAsync(retry: true);
            foreach (var bucket in buckets)
            {
                await AppendResultStorageService.CompactAsync<TestSubjectRecord>(
                    Options.Value.CsvRecordTableNamePrefix,
                    CsvResultStorage.ResultContainerName,
                    bucket);
            }

            await AppendResultStorageService.DeleteAsync(Options.Value.CsvRecordTableNamePrefix);
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
                    .Add(new SchemaV1<CsvCompactMessage<TestSubjectRecord>>(TestSubjectRecord.CsvCompactMessageSchemaName))
                    .Add(new SchemaV1<CleanupOrphanRecordsMessage<TestSubjectRecord>>(TestSubjectRecord.CleanupOrphanRecordsMessageSchemaName))
                    .Build());
                serviceCollection.AddCleanupOrphanRecordsService<TestCleanupOrphanRecordsAdapter, TestSubjectRecord>();
                serviceCollection.AddSingleton<ICsvResultStorage<TestSubjectRecord>, TestSubjectRecordStorage>();
                serviceCollection.AddSingleton<IMessageProcessor<CsvCompactMessage<TestSubjectRecord>>, TaskStateMessageProcessor<CsvCompactMessage<TestSubjectRecord>>>();
                serviceCollection.AddSingleton<ITaskStateMessageProcessor<CsvCompactMessage<TestSubjectRecord>>, CsvCompactProcessor<TestSubjectRecord>>();
                serviceCollection.AddSingleton(x =>
                {
                    var resultStorage = x.GetRequiredService<ICsvResultStorage<TestSubjectRecord>>();
                    return new CsvRecordContainerInfo(
                        resultStorage.ResultContainerName,
                        typeof(TestSubjectRecord),
                        CsvRecordStorageService.CompactPrefix);
                });
            });
        }

        private async Task AssertOwnerToSubjectAsync(string testName, string stepName, Func<byte[], string> deserializeEntity)
        {
            await AssertOwnerToSubjectAsync(OwnerToSubjectTableName, testName, stepName, deserializeEntity);
        }

        private async Task AssertSubjectToOwnerAsync(string testName, string stepName)
        {
            await AssertSubjectToOwnerAsync(SubjectToOwnerTableName, testName, stepName);
        }

        public CleanupOrphanRecordsServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            ResultContainerName = StoragePrefix + "1tsr1";
            OwnerToSubjectTableName = StoragePrefix + "1o2s1";
            SubjectToOwnerTableName = StoragePrefix + "1s2o1";
        }
    }
}
