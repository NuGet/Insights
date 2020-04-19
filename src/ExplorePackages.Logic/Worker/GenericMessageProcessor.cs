using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class GenericMessageProcessor
    {
        private readonly MessageSerializer _messageSerializer;
        private readonly IServiceProvider _serviceProvider;

        public GenericMessageProcessor(
            MessageSerializer messageSerializer,
            IServiceProvider serviceProvider)
        {
            _messageSerializer = messageSerializer;
            _serviceProvider = serviceProvider;
        }

        public async Task ProcessAsync(string message)
        {
            var deserializedMessage = _messageSerializer.Deserialize(message);
            var messageType = deserializedMessage.GetType();
            var processorType = typeof(IMessageProcessor<>).MakeGenericType(deserializedMessage.GetType());
            var processor = _serviceProvider.GetService(processorType);

            if (processor == null)
            {
                throw new NotSupportedException($"The message type '{deserializedMessage.GetType().FullName}' is not supported.");
            }

            var method = processorType.GetMethod(nameof(IMessageProcessor<object>.ProcessAsync), new Type[] { messageType });

            await (Task)method.Invoke(processor, new object[] { deserializedMessage });
        }
    }
}
