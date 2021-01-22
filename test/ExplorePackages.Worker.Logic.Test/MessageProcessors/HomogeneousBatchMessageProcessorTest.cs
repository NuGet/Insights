using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class HomogeneousBatchMessageProcessorTest
    {
        [Fact]
        public async Task ProcessesSuccessfulBatch()
        {
            await Target.ProcessAsync(Batch, dequeueCount: 1);

            GenericMessageProcessor.Verify(
                x => x.ProcessAsync(Batch.SchemaName, Batch.SchemaVersion, Batch.Messages, 1),
                Times.Once);
            Assert.Single(GenericMessageProcessor.Invocations);
            Assert.Empty(RawMessageEnqueuer.Invocations);
        }

        [Fact]
        public async Task ProcessesFailedAndTryAgainLasterBatch()
        {
            Result = new BatchMessageProcessorResult<JToken>(
                failed: new JToken[] { "c", "b" },
                tryAgainLater: new (JToken, TimeSpan)[]
                {
                    ("a", TimeSpan.FromMinutes(5)),
                    ("d", TimeSpan.FromMinutes(1)),
                });

            await Target.ProcessAsync(Batch, dequeueCount: 1);

            GenericMessageProcessor.Verify(
                x => x.ProcessAsync(Batch.SchemaName, Batch.SchemaVersion, Batch.Messages, 1),
                Times.Once);

            Assert.Equal(3, EnqueuedMessageBatches.Count);
            Assert.Equal(new[] { @"{""n"":""mymsg"",""v"":42,""d"":""c""}", @"{""n"":""mymsg"",""v"":42,""d"":""b""}" }, EnqueuedMessageBatches[0].ToArray());
            Assert.Equal(new[] { @"{""n"":""mymsg"",""v"":42,""d"":""d""}" }, EnqueuedMessageBatches[1].ToArray());
            Assert.Equal(new[] { @"{""n"":""mymsg"",""v"":42,""d"":""a""}" }, EnqueuedMessageBatches[2].ToArray());
            RawMessageEnqueuer.Verify(x => x.AddAsync(It.Is<IReadOnlyList<string>>(y => y.Count == 2)), Times.Once);
            RawMessageEnqueuer.Verify(x => x.AddAsync(It.Is<IReadOnlyList<string>>(y => y.Count == 1), TimeSpan.FromMinutes(1)), Times.Once);
            RawMessageEnqueuer.Verify(x => x.AddAsync(It.Is<IReadOnlyList<string>>(y => y.Count == 1), TimeSpan.FromMinutes(5)), Times.Once);
            Assert.Single(GenericMessageProcessor.Invocations);
            Assert.Equal(3, RawMessageEnqueuer.Invocations.Count);
        }

        [Fact]
        public async Task SplitsMessagesWhenDequeueCountIfGreaterThan1()
        {
            await Target.ProcessAsync(Batch, dequeueCount: 2);

            var messages = Assert.Single(EnqueuedMessageBatches);
            Assert.Equal(
                new[]
                {
                    @"{""n"":""mymsg"",""v"":42,""d"":""a""}",
                    @"{""n"":""mymsg"",""v"":42,""d"":""b""}",
                    @"{""n"":""mymsg"",""v"":42,""d"":""c""}",
                    @"{""n"":""mymsg"",""v"":42,""d"":""d""}",
                    @"{""n"":""mymsg"",""v"":42,""d"":""e""}",
                },
                messages.ToArray());
            Assert.Empty(GenericMessageProcessor.Invocations);
            Assert.Equal(1, RawMessageEnqueuer.Invocations.Count);
        }

        public HomogeneousBatchMessageProcessorTest(ITestOutputHelper output)
        {
            GenericMessageProcessor = new Mock<IGenericMessageProcessor>();
            RawMessageEnqueuer = new Mock<IRawMessageEnqueuer>();

            Batch = new HomogeneousBatchMessage
            {
                SchemaName = "mymsg",
                SchemaVersion = 42,
                Messages = new List<JToken> { "a", "b", "c", "d", "e" },
            };
            EnqueuedMessageBatches = new List<IReadOnlyList<string>>();
            Result = BatchMessageProcessorResult<JToken>.Empty;

            GenericMessageProcessor
                .Setup(x => x.ProcessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<JToken>>(), It.IsAny<int>()))
                .ReturnsAsync(() => Result);
            RawMessageEnqueuer
                .Setup(x => x.AddAsync(It.IsAny<IReadOnlyList<string>>()))
                .Returns(Task.CompletedTask)
                .Callback<IReadOnlyList<string>>(x => EnqueuedMessageBatches.Add(x));
            RawMessageEnqueuer
                .Setup(x => x.AddAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask)
                .Callback<IReadOnlyList<string>, TimeSpan>((x, _) => EnqueuedMessageBatches.Add(x));

            Target = new HomogeneousBatchMessageProcessor(
                GenericMessageProcessor.Object,
                RawMessageEnqueuer.Object,
                output.GetLogger<HomogeneousBatchMessageProcessor>());
        }

        public Mock<IGenericMessageProcessor> GenericMessageProcessor { get; }
        public Mock<IRawMessageEnqueuer> RawMessageEnqueuer { get; }
        public HomogeneousBatchMessage Batch { get; }
        public List<IReadOnlyList<string>> EnqueuedMessageBatches { get; }
        public BatchMessageProcessorResult<JToken> Result { get; set; }
        public HomogeneousBatchMessageProcessor Target { get; }
    }
}
