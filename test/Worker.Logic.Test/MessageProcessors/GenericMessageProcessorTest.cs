// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class GenericMessageProcessorTest
    {
        public class WithBatchProcessor : GenericMessageProcessorTest
        {
            public class ProcessSingleAsync : WithBatchProcessor
            {
                [Fact]
                public async Task Success()
                {
                    // Arrange & Act
                    await Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1);

                    // Assert
                    var batch = Assert.Single(ProcessedBatches);
                    var message = Assert.Single(batch);
                    Assert.Equal(SingleMessage, SchemaSerializer.Serialize(message).AsString());

                    MessageProcessor.Verify(x => x.ProcessAsync(batch, 1), Times.Once);

                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Empty(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task Exception()
                {
                    // Arrange
                    var expected = new InvalidOperationException("Processing this message failed for some reason.");
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .ThrowsAsync(expected);

                    // Act
                    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1));

                    // Assert
                    Assert.Same(expected, ex);
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Empty(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task FailureSameMessage()
                {
                    // Arrange
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .Returns<IReadOnlyList<CatalogLeafScanMessage>, long>((m, _) => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScanMessage>(m)));

                    // Act
                    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1));

                    // Assert
                    Assert.Equal("A batch containing a single message failed.", ex.Message);
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Empty(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task FailureDifferentMessage()
                {
                    // Arrange
                    var different = new CatalogLeafScanMessage { StorageSuffix = "different" };
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .Returns<IReadOnlyList<CatalogLeafScanMessage>, long>((m, _) => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScanMessage>(
                            failed: new[] { different })));

                    // Act
                    await Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>()), Times.Once);
                    Assert.Equal(SchemaSerializer.Serialize(different).AsString(), Assert.Single(Assert.Single(EnqueuedBatches)));
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task TryAgainLaterSameMessage()
                {
                    // Arrange
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .Returns<IReadOnlyList<CatalogLeafScanMessage>, long>((m, _) => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScanMessage>(
                            failed: Array.Empty<CatalogLeafScanMessage>(),
                            tryAgainLater: m,
                            notBefore: TimeSpan.FromMinutes(3))));

                    // Act
                    await Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>(), TimeSpan.FromMinutes(3)), Times.Once);
                    Assert.Equal(SingleMessage, Assert.Single(Assert.Single(EnqueuedBatches)));
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task TryAgainLaterDifferentMessage()
                {
                    // Arrange
                    var different = new CatalogLeafScanMessage { StorageSuffix = "different" };
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .Returns<IReadOnlyList<CatalogLeafScanMessage>, long>((m, _) => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScanMessage>(
                            failed: Array.Empty<CatalogLeafScanMessage>(),
                            tryAgainLater: new[] { different },
                            notBefore: TimeSpan.FromMinutes(3))));

                    // Act
                    await Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>(), TimeSpan.FromMinutes(3)), Times.Once);
                    Assert.Equal(SchemaSerializer.Serialize(different).AsString(), Assert.Single(Assert.Single(EnqueuedBatches)));
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                public ProcessSingleAsync(ITestOutputHelper output) : base(output)
                {
                }
            }

            public class ProcessBatchAsync : WithBatchProcessor
            {
                [Fact]
                public async Task Success()
                {
                    // Arrange & Act
                    await Target.ProcessBatchAsync(SchemaName, SchemaVersion, MessageBatch, dequeueCount: 1);

                    // Assert
                    var batch = Assert.Single(ProcessedBatches);
                    Assert.Equal(MessageBatch.Count, batch.Count);
                    Assert.Equal(MessageBatch, GetData(batch));

                    MessageProcessor.Verify(x => x.ProcessAsync(batch, 1), Times.Once);

                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Empty(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task Exception()
                {
                    // Arrange
                    var expected = new InvalidOperationException("Processing this message failed for some reason.");
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .ThrowsAsync(expected);

                    // Act
                    await Target.ProcessBatchAsync(SchemaName, SchemaVersion, MessageBatch, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>()), Times.Once);
                    Assert.Equal(GetString(MessageBatch), Assert.Single(EnqueuedBatches));
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task FailuresSameMessages()
                {
                    // Arrange
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .Returns<IReadOnlyList<CatalogLeafScanMessage>, long>((m, _) => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScanMessage>(
                            failed: m.Take(2))));

                    // Act
                    await Target.ProcessBatchAsync(SchemaName, SchemaVersion, MessageBatch, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>()), Times.Once);
                    Assert.Equal(GetString(MessageBatch.Take(2)), Assert.Single(EnqueuedBatches));
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task FailureDifferentMessage()
                {
                    // Arrange
                    var differentA = new CatalogLeafScanMessage { StorageSuffix = "differentA" };
                    var differentB = new CatalogLeafScanMessage { StorageSuffix = "differentB" };
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .Returns<IReadOnlyList<CatalogLeafScanMessage>, long>((m, _) => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScanMessage>(
                            failed: new[] { differentA, differentB })));

                    // Act
                    await Target.ProcessBatchAsync(SchemaName, SchemaVersion, MessageBatch, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>()), Times.Once);
                    Assert.Equal(GetString(new[] { differentA, differentB }), Assert.Single(EnqueuedBatches));
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task TryAgainLaterSameMessages()
                {
                    // Arrange
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .Returns<IReadOnlyList<CatalogLeafScanMessage>, long>((m, _) => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScanMessage>(
                            failed: Array.Empty<CatalogLeafScanMessage>(),
                            tryAgainLater: m.Take(2),
                            notBefore: TimeSpan.FromMinutes(3))));

                    // Act
                    await Target.ProcessBatchAsync(SchemaName, SchemaVersion, MessageBatch, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>(), TimeSpan.FromMinutes(3)), Times.Once);
                    Assert.Equal(GetString(MessageBatch.Take(2)), Assert.Single(EnqueuedBatches));
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task TryAgainLaterDifferentMessage()
                {
                    // Arrange
                    var differentA = new CatalogLeafScanMessage { StorageSuffix = "differentA" };
                    var differentB = new CatalogLeafScanMessage { StorageSuffix = "differentB" };
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                        .Returns<IReadOnlyList<CatalogLeafScanMessage>, long>((m, _) => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScanMessage>(
                            failed: Array.Empty<CatalogLeafScanMessage>(),
                            tryAgainLater: new[] { differentA, differentB },
                            notBefore: TimeSpan.FromMinutes(3))));

                    // Act
                    await Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>(), TimeSpan.FromMinutes(3)), Times.Once);
                    Assert.Equal(GetString(new[] { differentA, differentB }), Assert.Single(EnqueuedBatches));
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                public ProcessBatchAsync(ITestOutputHelper output) : base(output)
                {
                }
            }

            public WithBatchProcessor(ITestOutputHelper output) : base(output)
            {
                MessageProcessor = new Mock<IBatchMessageProcessor<CatalogLeafScanMessage>>();
                Result = BatchMessageProcessorResult<CatalogLeafScanMessage>.Empty;
                ProcessedBatches = new List<IReadOnlyList<CatalogLeafScanMessage>>();

                MessageProcessor
                    .Setup(x => x.ProcessAsync(It.IsAny<IReadOnlyList<CatalogLeafScanMessage>>(), It.IsAny<long>()))
                    .ReturnsAsync(() => Result)
                    .Callback<IReadOnlyList<CatalogLeafScanMessage>, long>((x, _) => ProcessedBatches.Add(x));

                ServiceProvider
                    .Setup(x => x.GetService(typeof(IBatchMessageProcessor<CatalogLeafScanMessage>)))
                    .Returns(() => MessageProcessor.Object);
            }

            public Mock<IBatchMessageProcessor<CatalogLeafScanMessage>> MessageProcessor { get; }
            public BatchMessageProcessorResult<CatalogLeafScanMessage> Result { get; }
            public List<IReadOnlyList<CatalogLeafScanMessage>> ProcessedBatches { get; }
        }
        public class WithNonBatchProcessor : GenericMessageProcessorTest
        {
            public class ProcessSingleAsync : WithNonBatchProcessor
            {
                [Fact]
                public async Task Success()
                {
                    // Arrange & Act
                    await Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1);

                    // Assert
                    var message = Assert.Single(ProcessedMessages);
                    Assert.Equal(SingleMessage, SchemaSerializer.Serialize(message).AsString());

                    MessageProcessor.Verify(x => x.ProcessAsync(message, 1), Times.Once);

                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Empty(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task Exception()
                {
                    // Arrange
                    var expected = new InvalidOperationException("Processing this message failed for some reason.");
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<CatalogLeafScanMessage>(), It.IsAny<long>()))
                        .ThrowsAsync(expected);

                    // Act
                    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ProcessSingleAsync(QueueType.Work, SingleMessage, dequeueCount: 1));

                    // Assert
                    Assert.Same(expected, ex);
                    Assert.Single(MessageProcessor.Invocations);
                    Assert.Empty(RawMessageEnqueuer.Invocations);
                }

                public ProcessSingleAsync(ITestOutputHelper output) : base(output)
                {
                }
            }

            public class ProcessBatchAsync : WithNonBatchProcessor
            {
                [Fact]
                public async Task Success()
                {
                    // Arrange & Act
                    await Target.ProcessBatchAsync(SchemaName, SchemaVersion, MessageBatch, dequeueCount: 1);

                    // Assert
                    Assert.Equal(MessageBatch.Count, ProcessedMessages.Count);
                    Assert.Equal(MessageBatch, GetData(ProcessedMessages));

                    MessageProcessor.Verify(x => x.ProcessAsync(It.IsAny<CatalogLeafScanMessage>(), 1), Times.Exactly(MessageBatch.Count));

                    Assert.Equal(MessageBatch.Count, MessageProcessor.Invocations.Count);
                    Assert.Empty(RawMessageEnqueuer.Invocations);
                }

                [Fact]
                public async Task Exception()
                {
                    // Arrange
                    var expected = new InvalidOperationException("Processing this message failed for some reason.");
                    MessageProcessor
                        .Setup(x => x.ProcessAsync(It.IsAny<CatalogLeafScanMessage>(), It.IsAny<long>()))
                        .ThrowsAsync(expected);

                    // Act
                    await Target.ProcessBatchAsync(SchemaName, SchemaVersion, MessageBatch, dequeueCount: 1);

                    // Assert
                    RawMessageEnqueuer.Verify(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>()), Times.Once);
                    Assert.Equal(GetString(MessageBatch), Assert.Single(EnqueuedBatches));
                    Assert.Equal(MessageBatch.Count, MessageProcessor.Invocations.Count);
                    Assert.Single(RawMessageEnqueuer.Invocations);
                }

                public ProcessBatchAsync(ITestOutputHelper output) : base(output)
                {
                }
            }

            public WithNonBatchProcessor(ITestOutputHelper output) : base(output)
            {
                MessageProcessor = new Mock<IMessageProcessor<CatalogLeafScanMessage>>();
                ProcessedMessages = new List<CatalogLeafScanMessage>();

                MessageProcessor
                    .Setup(x => x.ProcessAsync(It.IsAny<CatalogLeafScanMessage>(), It.IsAny<long>()))
                    .Returns(Task.CompletedTask)
                    .Callback<CatalogLeafScanMessage, long>((x, _) => ProcessedMessages.Add(x));

                ServiceProvider
                    .Setup(x => x.GetService(typeof(IMessageProcessor<CatalogLeafScanMessage>)))
                    .Returns(() => MessageProcessor.Object);
            }

            public Mock<IMessageProcessor<CatalogLeafScanMessage>> MessageProcessor { get; }
            public List<CatalogLeafScanMessage> ProcessedMessages { get; }
        }

        public GenericMessageProcessorTest(ITestOutputHelper output)
        {
            SchemaSerializer = new SchemaSerializer(output.GetLogger<SchemaSerializer>());
            ServiceProvider = new Mock<IServiceProvider>();
            RawMessageEnqueuer = new Mock<IRawMessageEnqueuer>();

            var serializer = SchemaSerializer.GetSerializer<CatalogLeafScanMessage>();
            var message = serializer.SerializeMessage(MakeMessage());
            SingleMessage = message.AsString();
            SchemaName = serializer.Name;
            SchemaVersion = serializer.LatestVersion;
            MessageBatch = Enumerable
                .Range(0, 3)
                .Select(MakeMessage)
                .Select(serializer.SerializeData)
                .Select(x => x.AsJToken()).ToList();
            EnqueuedBatches = new List<IReadOnlyList<string>>();

            RawMessageEnqueuer
                .Setup(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>()))
                .Returns(Task.CompletedTask)
                .Callback<QueueType, IReadOnlyList<string>>((_, m) => EnqueuedBatches.Add(m));
            RawMessageEnqueuer
                .Setup(x => x.AddAsync(QueueType.Work, It.IsAny<IReadOnlyList<string>>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask)
                .Callback<QueueType, IReadOnlyList<string>, TimeSpan>((_, m, __) => EnqueuedBatches.Add(m));

            Target = new GenericMessageProcessor(
                SchemaSerializer,
                ServiceProvider.Object,
                RawMessageEnqueuer.Object,
                output.GetTelemetryClient(),
                output.GetLogger<GenericMessageProcessor>());
        }

        public JToken GetData(CatalogLeafScanMessage message)
        {
            return SchemaSerializer.GetSerializer<CatalogLeafScanMessage>().SerializeData(message).AsJToken();
        }

        public string GetString(CatalogLeafScanMessage message)
        {
            return SchemaSerializer.GetSerializer<CatalogLeafScanMessage>().SerializeMessage(message).AsString();
        }

        public string GetString(JToken data)
        {
            return NameVersionSerializer.SerializeMessage(SchemaName, SchemaVersion, data).AsString();
        }

        public IReadOnlyList<JToken> GetData(IEnumerable<CatalogLeafScanMessage> input)
        {
            return input.Select(GetData).ToList();
        }

        public IReadOnlyList<string> GetString(IEnumerable<CatalogLeafScanMessage> input)
        {
            return input.Select(GetString).ToList();
        }

        public IReadOnlyList<string> GetString(IEnumerable<JToken> input)
        {
            return input.Select(GetString).ToList();
        }

        private static CatalogLeafScanMessage MakeMessage(int i = 0)
        {
            return new CatalogLeafScanMessage
            {
                StorageSuffix = "ss",
                ScanId = "si",
                PageId = "pi",
                LeafId = "li-" + i,
            };
        }

        public SchemaSerializer SchemaSerializer { get; }
        public Mock<IServiceProvider> ServiceProvider { get; }
        public Mock<IRawMessageEnqueuer> RawMessageEnqueuer { get; }
        public string SingleMessage { get; }
        public string SchemaName { get; }
        public int SchemaVersion { get; }
        public List<JToken> MessageBatch { get; }
        public List<IReadOnlyList<string>> EnqueuedBatches { get; }

        public GenericMessageProcessor Target { get; }
    }
}
