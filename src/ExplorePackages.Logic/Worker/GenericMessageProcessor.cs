using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class GenericMessageProcessor
    {
        private readonly MessageSerializer _messageSerializer;
        private readonly IMessageProcessor<PackageQueryMessage> _packageQuery;

        public GenericMessageProcessor(
            MessageSerializer messageSerializer,
            IMessageProcessor<PackageQueryMessage> packageQuery)
        {
            _messageSerializer = messageSerializer;
            _packageQuery = packageQuery;
        }

        public async Task ProcessAsync(byte[] message)
        {
            var deserializedMessage = _messageSerializer.Deserialize(message);

            switch (deserializedMessage)
            {
                case PackageQueryMessage packageQueryMessage:
                    await _packageQuery.ProcessAsync(packageQueryMessage);
                    break;
                default:
                    throw new NotSupportedException($"The message type '{deserializedMessage.GetType().FullName}' is not supported.");
            }
        }
    }
}
