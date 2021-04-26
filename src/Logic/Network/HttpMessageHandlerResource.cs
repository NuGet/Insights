using System;
using System.Net.Http;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages
{
    /// <summary>
    /// An implementation of <see cref="HttpMessageHandler"/> that does not require provide an
    /// <see cref="HttpClientHandler"/>, since this type is not needed for <see cref="NuGet.Protocol.HttpSource"/>.
    /// </summary>
    public class HttpMessageHandlerResource : HttpHandlerResource
    {
        public HttpMessageHandlerResource(HttpMessageHandler messageHandler)
        {
            MessageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        }

        public override HttpClientHandler ClientHandler => throw new NotImplementedException();
        public override HttpMessageHandler MessageHandler { get; }
    }
}
