using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class MessageEnqueuerTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task DoesNotBatchIfMessageCountIsLessThanThreshold(int messageCount)
        {
            await Target.EnqueueAsync(Enumerable.Range(0, messageCount).Select(x => new CatalogPageScanMessage { PageId = x.ToString() }).ToList());

            var messages = Assert.Single(EnqueuedMessages);
            for (var i = 0; i < messageCount; i++)
            {
                var message = (CatalogPageScanMessage)messages[i];
                Assert.Equal(i.ToString(), message.PageId);
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
            var perBatch = RawMessageEnqueuer.Object.BulkEnqueueStrategy.MaxSize / byteCount;
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
                Assert.StartsWith($"{i}_", message.Messages[i % perBatch].ToString());
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
            SchemaSerializer = new SchemaSerializer(output.GetLogger<SchemaSerializer>());
            Options = new Mock<IOptions<ExplorePackagesWorkerSettings>>();
            Settings = new ExplorePackagesWorkerSettings();
            MessageBatcher = new Mock<IMessageBatcher>();
            RawMessageEnqueuer = new Mock<IRawMessageEnqueuer>();

            Options.Setup(x => x.Value).Returns(() => Settings);

            RawMessageEnqueuer
                .Setup(x => x.BulkEnqueueStrategy)
                .Returns(() => BulkEnqueueStrategy.Enabled(3, 64 * 1024));

            RawMessageEnqueuer
                .Setup(x => x.AddAsync(It.IsAny<IReadOnlyList<string>>()))
                .Returns(Task.CompletedTask)
                .Callback<IReadOnlyList<string>>(x => EnqueuedMessages.Add(x.Select(y => SchemaSerializer.Deserialize(y)).ToList()));
            RawMessageEnqueuer
                .Setup(x => x.AddAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask)
                .Callback<IReadOnlyList<string>, TimeSpan>((x, ts) => EnqueuedMessages.Add(x.Select(y => SchemaSerializer.Deserialize(y)).ToList()));

            EnqueuedMessages = new List<List<object>>();

            Target = new MessageEnqueuer(
                SchemaSerializer,
                MessageBatcher.Object,
                RawMessageEnqueuer.Object,
                output.GetLogger<MessageEnqueuer>());
        }

        public SchemaSerializer SchemaSerializer { get; }
        public Mock<IOptions<ExplorePackagesWorkerSettings>> Options { get; }
        public ExplorePackagesWorkerSettings Settings { get; }
        public Mock<IMessageBatcher> MessageBatcher { get; }
        public Mock<IRawMessageEnqueuer> RawMessageEnqueuer { get; }
        public List<List<object>> EnqueuedMessages { get; }
        public MessageEnqueuer Target { get; }
    }
}
