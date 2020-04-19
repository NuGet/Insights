using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class MessageEnqueuerTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task DoesNotBatchIfMessageCountIsLessThanThreshold(int messageCount)
        {
            await Target.EnqueueAsync(
                Enumerable.Range(0, messageCount).ToList(),
                i => MessageSerializer.Serialize(new CatalogPageScanMessage { Url = i.ToString() }));

            Assert.Equal(messageCount, EnqueuedMessages.Count);
            for (var i = 0; i < messageCount; i++)
            {
                var message = (CatalogPageScanMessage)EnqueuedMessages[i];
                Assert.Equal(i.ToString(), message.Url);
            }
        }

        [Theory]
        [InlineData(100)]
        [InlineData(101)]
        [InlineData(102)]
        [InlineData(103)]
        public async Task BatchesIfMessageCountIsGreaterThanThreshold(int messageCount)
        {
            var byteCount = 10000;
            var perBatch = 6;

            await Target.EnqueueAsync(
                Enumerable.Range(0, messageCount).ToList(),
                i => GetSerializedMessage(i, byteCount));

            Assert.Equal((messageCount / perBatch) + (messageCount % perBatch > 0 ? 1 : 0), EnqueuedMessages.Count);
            for (var i = 0; i < messageCount; i++)
            {
                var message = (BulkEnqueueMessage)EnqueuedMessages[i / perBatch];
                Assert.StartsWith($"{i}_", message.Messages[i % perBatch].ToString());
            }
        }

        private SerializedMessage GetSerializedMessage(int id, int byteCount)
        {
            return new SerializedMessage(
                () => string.Join(
                    string.Empty,
                    Enumerable.Repeat($"{id}_", byteCount / 2)).Substring(0, byteCount - 2)); // subtract 2 for JSON quotes
        }

        public MessageEnqueuerTest(ITestOutputHelper output)
        {
            MessageSerializer = new MessageSerializer(output.GetLogger<MessageSerializer>());
            RawMessageEnqueuer = new Mock<IRawMessageEnqueuer>();

            RawMessageEnqueuer
                .Setup(x => x.AddAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback<string>(x => EnqueuedMessages.Add(MessageSerializer.Deserialize(x)));

            EnqueuedMessages = new List<object>();

            Target = new MessageEnqueuer(
                MessageSerializer,
                RawMessageEnqueuer.Object,
                output.GetLogger<MessageEnqueuer>());
        }

        public MessageSerializer MessageSerializer { get; }
        public Mock<IRawMessageEnqueuer> RawMessageEnqueuer { get; }
        public List<object> EnqueuedMessages { get; }
        public MessageEnqueuer Target { get; }
    }
}
