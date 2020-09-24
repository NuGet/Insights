using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class GenericMessageProcessor
    {
        private readonly SchemaSerializer _serializer;
        private readonly IServiceProvider _serviceProvider;

        public GenericMessageProcessor(
            SchemaSerializer serializer,
            IServiceProvider serviceProvider)
        {
            _serializer = serializer;
            _serviceProvider = serviceProvider;
        }

        public async Task ProcessAsync(string message)
        {
            var deserializedMessage = _serializer.Deserialize(message);
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
