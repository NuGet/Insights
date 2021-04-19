using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class MessageEnqueuerIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public MessageEnqueuerIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public IMessageEnqueuer Target => Host.Services.GetRequiredService<IMessageEnqueuer>();

        [Fact]
        public async Task CanEnqueueAscii()
        {
            await Target.InitializeAsync();
            var messages = new[] { new CatalogLeafScanMessage { LeafId = "Newtonsoft" } };

            await Target.EnqueueAsync(QueueType.Work, messages);

            PeekedMessage message = await (await ServiceClientFactory.GetQueueServiceClientAsync())
                .GetQueueClient(Options.Value.WorkQueueName)
                .PeekMessageAsync();
            Assert.Equal(@"{""n"":""cls"",""v"":1,""d"":{""r"":""Newtonsoft""}}", message.Body.ToString());
        }

        [Fact]
        public async Task CanEnqueueNonAscii()
        {
            await Target.InitializeAsync();
            var messages = new[] { new CatalogLeafScanMessage { LeafId = "Nëwtönsöft" } };

            await Target.EnqueueAsync(QueueType.Work, messages);

            PeekedMessage message = await (await ServiceClientFactory.GetQueueServiceClientAsync())
                .GetQueueClient(Options.Value.WorkQueueName)
                .PeekMessageAsync();
            Assert.Equal(@"{""n"":""cls"",""v"":1,""d"":{""r"":""Nëwtönsöft""}}", message.Body.ToString());
        }
    }
}
