// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;

namespace NuGet.Insights.Worker
{
    public class MessageEnqueuerTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task DoesNotBatchIfMessageCountIsLessThanThreshold(int messageCount)
        {
            await Target.EnqueueAsync(Enumerable.Range(0, messageCount).Select(x => new CatalogPageScanMessage { PageId = x.ToString(CultureInfo.InvariantCulture) }).ToList());

            var messages = Assert.Single(EnqueuedMessages);
            for (var i = 0; i < messageCount; i++)
            {
                var message = (CatalogPageScanMessage)messages[i];
                Assert.Equal(i.ToString(CultureInfo.InvariantCulture), message.PageId);
            }
        }

        [Theory]
        [InlineData(3)]
        [InlineData(100)]
        [InlineData(101)]
        [InlineData(102)]
        [InlineData(103)]
        public async Task BatchesIfMessageCountIsGreaterThanThreshold(int messageCount)
        {
            var byteCount = 10000;
            var perBatch = RawMessageEnqueuer.Object.MaxMessageSize / byteCount;
            Assert.Equal(6, perBatch);

            var schema = new SchemaV1<string>("i");

            await Target.EnqueueAsync(
                Enumerable.Range(0, messageCount).Select(i => GetSerializedMessage(i, byteCount)).ToList(),
                schema,
                TimeSpan.Zero);

            Assert.Equal((messageCount / perBatch) + (messageCount % perBatch > 0 ? 1 : 0), EnqueuedMessages.Count);
            for (var i = 0; i < messageCount; i++)
            {
                var message = (HomogeneousBulkEnqueueMessage)Assert.Single(EnqueuedMessages[i / perBatch]);
                Assert.StartsWith($"{i}_", message.Messages[i % perBatch].ToString(), StringComparison.Ordinal);
            }
        }

        [Theory]
        [MemberData(nameof(UsesCorrectQueueByMessageTypeData))]
        public async Task UsesCorrectQueueByMessageType(Type messageType, QueueType queue)
        {
            var parameters = Array.CreateInstance(messageType, 1);
            parameters.SetValue(Activator.CreateInstance(messageType), 0);

            await (Task)Target
                .GetType()
                .GetMethods()
                .Single(x => x.Name == nameof(MessageEnqueuer.EnqueueAsync) && x.GetParameters().Length == 1)
                .MakeGenericMethod(messageType)
                .Invoke(Target, new[] { parameters });

            RawMessageEnqueuer.Verify(
                x => x.AddAsync(queue, It.Is<IReadOnlyList<string>>(y => y.Count == 1), TimeSpan.Zero),
                Times.Once);
        }

        public static IEnumerable<object[]> UsesCorrectQueueByMessageTypeData => SchemaCollectionBuilder
            .DefaultMessageSchemas
            .Select(x => x.GetType().GenericTypeArguments.Single())
            .Select(x => new object[] { x, ExpandMessageTypes.Contains(x) ? QueueType.Expand : QueueType.Work });

        [Theory]
        [MemberData(nameof(UsesCorrectQueueBySchemaNameData))]
        public void UsesCorrectQueueBySchemaName(string schemaName, QueueType queue)
        {
            Assert.Equal(queue, Target.GetQueueType(schemaName));
        }

        public static IEnumerable<object[]> UsesCorrectQueueBySchemaNameData => SchemaCollectionBuilder
            .DefaultMessageSchemas
            .Cast<ISchemaDeserializer>()
            .Select(x => x.Name)
            .Select(x => new object[] { x, ExpandSchemaNames.Contains(x) ? QueueType.Expand : QueueType.Work });

        public static HashSet<Type> ExpandMessageTypes => new HashSet<Type>
        {
            typeof(HomogeneousBulkEnqueueMessage),
            typeof(TableScanMessage<CatalogLeafScan>),
            typeof(TableScanMessage<LatestPackageLeaf>),
            typeof(TableScanMessage<BucketedPackage>),
        };

        public static HashSet<string> ExpandSchemaNames
        {
            get
            {
                var schemaSerializer = new SchemaSerializer(
                    SchemaCollectionBuilder.Default.Build(),
                    NullLogger<SchemaSerializer>.Instance);
                return ExpandMessageTypes
                    .Select(x => schemaSerializer.GetGenericSerializer(x).Name)
                    .ToHashSet();
            }
        }

        private string GetSerializedMessage(int id, int byteCount)
        {
            return string.Join(
                string.Empty,
                Enumerable.Repeat($"{id}_", byteCount / 2)).Substring(0, byteCount - 2); // subtract 2 for JSON quotes
        }

        public MessageEnqueuerTest(ITestOutputHelper output)
        {
            SchemaSerializer = new SchemaSerializer(
                SchemaCollectionBuilder.Default.Build(),
                output.GetLogger<SchemaSerializer>());
            Options = new Mock<IOptions<NuGetInsightsWorkerSettings>>();
            Settings = new NuGetInsightsWorkerSettings();
            MessageBatcher = new Mock<IMessageBatcher>();
            RawMessageEnqueuer = new Mock<IRawMessageEnqueuer>();

            Options.Setup(x => x.Value).Returns(() => Settings);

            RawMessageEnqueuer
                .Setup(x => x.BulkEnqueueStrategy)
                .Returns(() => BulkEnqueueStrategy.Enabled(3));
            RawMessageEnqueuer
                .Setup(x => x.MaxMessageSize)
                .Returns(() => 64 * 1024);

            RawMessageEnqueuer
                .Setup(x => x.AddAsync(It.IsAny<QueueType>(), It.IsAny<IReadOnlyList<string>>()))
                .Returns(Task.CompletedTask)
                .Callback<QueueType, IReadOnlyList<string>>((_, x) => EnqueuedMessages.Add(x.Select(y => SchemaSerializer.Deserialize(y).Data).ToList()));
            RawMessageEnqueuer
                .Setup(x => x.AddAsync(It.IsAny<QueueType>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask)
                .Callback<QueueType, IReadOnlyList<string>, TimeSpan>((_, x, __) => EnqueuedMessages.Add(x.Select(y => SchemaSerializer.Deserialize(y).Data).ToList()));

            EnqueuedMessages = new List<List<object>>();

            Target = new MessageEnqueuer(
                SchemaSerializer,
                MessageBatcher.Object,
                RawMessageEnqueuer.Object,
                output.GetTelemetryClient(),
                output.GetLogger<MessageEnqueuer>());
        }

        public SchemaSerializer SchemaSerializer { get; }
        public Mock<IOptions<NuGetInsightsWorkerSettings>> Options { get; }
        public NuGetInsightsWorkerSettings Settings { get; }
        public Mock<IMessageBatcher> MessageBatcher { get; }
        public Mock<IRawMessageEnqueuer> RawMessageEnqueuer { get; }
        public List<List<object>> EnqueuedMessages { get; }
        public MessageEnqueuer Target { get; }
    }
}
